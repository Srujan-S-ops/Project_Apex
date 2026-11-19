package com.apex.engine.race;

import com.apex.engine.net.dto.Dtos;

import java.util.ArrayList;
import java.util.Comparator;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.NavigableMap;
import java.util.TreeMap;

/**
 * Live standings. Gaps are time-based: for each driver the engine remembers when it reached each
 * distance; a gap is "now" minus the time the car ahead passed the same distance.
 * Not thread-safe on its own — always used under the owning RaceSession lock.
 */
final class Leaderboard {
    private static final int MAX_SAMPLES = 6000;
    private final Map<Integer, NavigableMap<Double, Double>> timeAtDistance = new HashMap<>();

    void record(int driverId, double distance, double raceTime) {
        NavigableMap<Double, Double> m = timeAtDistance.computeIfAbsent(driverId, k -> new TreeMap<>());
        Map.Entry<Double, Double> last = m.lastEntry();
        if (last == null || distance > last.getKey() + 0.5) {
            m.put(distance, raceTime);
            if (m.size() > MAX_SAMPLES) {
                m.pollFirstEntry();
            }
        }
    }

    /** Time at which the given driver passed {@code distance}, linearly interpolated. */
    double timeAt(int driverId, double distance) {
        NavigableMap<Double, Double> m = timeAtDistance.get(driverId);
        if (m == null || m.isEmpty()) {
            return Double.NaN;
        }
        Map.Entry<Double, Double> lo = m.floorEntry(distance), hi = m.ceilingEntry(distance);
        if (lo == null) {
            return Double.NaN;
        }
        if (hi == null || hi.getKey().equals(lo.getKey())) {
            return lo.getValue();
        }
        double t = (distance - lo.getKey()) / (hi.getKey() - lo.getKey());
        return lo.getValue() + (hi.getValue() - lo.getValue()) * t;
    }

    List<Dtos.StandingDto> standings(Iterable<DriverState> drivers, double raceTime) {
        List<DriverState> sorted = new ArrayList<>();
        drivers.forEach(sorted::add);
        sorted.sort(Comparator.comparingDouble(DriverState::distance).reversed());
        List<Dtos.StandingDto> out = new ArrayList<>(sorted.size());
        DriverState leader = sorted.isEmpty() ? null : sorted.get(0);
        for (int i = 0; i < sorted.size(); i++) {
            DriverState d = sorted.get(i);
            float gapLeader = 0f, interval = 0f;
            if (i > 0) {
                gapLeader = gap(leader, d, raceTime);
                interval = gap(sorted.get(i - 1), d, raceTime);
            }
            out.add(new Dtos.StandingDto(d.id(), i + 1, d.lap(), gapLeader, interval, (float) d.bestLap()));
        }
        return out;
    }

    private float gap(DriverState ahead, DriverState behind, double raceTime) {
        double passed = timeAt(ahead.id(), behind.distance());
        if (Double.isNaN(passed)) {
            return 0f;
        }
        return (float) Math.max(0.0, raceTime - passed);
    }
}
