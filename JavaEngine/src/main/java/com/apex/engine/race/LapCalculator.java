package com.apex.engine.race;

import com.apex.engine.error.ProtocolException;
import com.apex.engine.net.dto.Dtos;

import java.util.Collection;
import java.util.OptionalDouble;

/** Lap and sector validation plus lap statistics. */
public final class LapCalculator {
    /** Faster than this average speed (m/s) is physically impossible for a GT3 → reject. */
    private static final double MAX_AVERAGE_SPEED = 95.0;
    private static final double SECTOR_TOLERANCE = 0.05;

    private LapCalculator() {
    }

    public static LapRecord validate(Dtos.LapCompleted m, double trackLength) {
        if (!Float.isFinite(m.lapTime()) || m.lapTime() <= 0 || m.lap() <= 0) {
            throw new ProtocolException("invalid lap time/lap number");
        }
        boolean sectorsPresent = m.s1() > 0 && m.s2() > 0 && m.s3() > 0;
        boolean consistent = sectorsPresent && Math.abs(m.s1() + m.s2() + m.s3() - m.lapTime()) <= SECTOR_TOLERANCE;
        boolean plausible = trackLength <= 0 || m.lapTime() >= trackLength / MAX_AVERAGE_SPEED;
        boolean valid = m.valid() && consistent && plausible;
        return new LapRecord(m.driverId(), m.lap(), m.lapTime(), m.s1(), m.s2(), m.s3(), valid);
    }

    public static OptionalDouble best(Collection<LapRecord> laps) {
        return laps.stream().filter(LapRecord::valid).mapToDouble(LapRecord::lapTime).min();
    }

    public static OptionalDouble average(Collection<LapRecord> laps) {
        return laps.stream().mapToDouble(LapRecord::lapTime).average();
    }

    /** Sum of the best individual sectors across valid laps. */
    public static OptionalDouble theoreticalBest(Collection<LapRecord> laps) {
        OptionalDouble b1 = laps.stream().filter(LapRecord::valid).mapToDouble(LapRecord::s1).min();
        OptionalDouble b2 = laps.stream().filter(LapRecord::valid).mapToDouble(LapRecord::s2).min();
        OptionalDouble b3 = laps.stream().filter(LapRecord::valid).mapToDouble(LapRecord::s3).min();
        if (b1.isEmpty() || b2.isEmpty() || b3.isEmpty()) {
            return OptionalDouble.empty();
        }
        return OptionalDouble.of(b1.getAsDouble() + b2.getAsDouble() + b3.getAsDouble());
    }
}
