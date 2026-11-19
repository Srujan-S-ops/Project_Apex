package com.apex.engine;

import com.apex.engine.ai.AICommand;
import com.apex.engine.ai.AIDecisionEngine;
import com.apex.engine.ai.TacticalMode;
import com.apex.engine.error.ProtocolException;
import com.apex.engine.error.SessionNotFoundException;
import com.apex.engine.net.dto.Dtos;
import com.apex.engine.race.LapCalculator;
import com.apex.engine.race.LapRecord;
import com.apex.engine.race.RaceSession;
import com.apex.engine.race.RaceSessionManager;
import com.apex.engine.track.TrackAnalyzer;
import com.apex.engine.util.RollingWindow;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.Test;

import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;

import static org.junit.jupiter.api.Assertions.*;

class RaceLogicTest {
    private final ExecutorService pool = Executors.newFixedThreadPool(4);

    @AfterEach
    void tearDown() {
        pool.shutdownNow();
    }

    static Dtos.SessionStart start(String id) {
        return new Dtos.SessionStart(id, "QuickRace", "Hard", "stadium", 3, 1050f, List.of(
                new Dtos.DriverDto(0, "VARGA", "Aggressive", false, "car"),
                new Dtos.DriverDto(1, "MOREAU", "Defensive", false, "car"),
                new Dtos.DriverDto(2, "ASAHARA", "Balanced", false, "car"),
                new Dtos.DriverDto(3, "LINDQVIST", "Balanced", false, "car"),
                new Dtos.DriverDto(4, "PLAYER", "Balanced", true, "car")));
    }

    static Dtos.Telemetry telemetry(String id, float t, float... distances) {
        List<Dtos.CarTelemetry> cars = new ArrayList<>();
        for (int i = 0; i < distances.length; i++) {
            cars.add(new Dtos.CarTelemetry(i, distances[i], distances[i] % 1050f, 0f, 50f, 1, i + 1));
        }
        return new Dtos.Telemetry(id, t, cars);
    }

    @Test
    void lapValidationChecksSectorConsistencyAndPlausibility() {
        LapRecord ok = LapCalculator.validate(new Dtos.LapCompleted("s", 0, 1, 60f, 20f, 20f, 20f, true), 1050);
        assertTrue(ok.valid());
        LapRecord badSectors = LapCalculator.validate(new Dtos.LapCompleted("s", 0, 1, 60f, 20f, 20f, 10f, true), 1050);
        assertFalse(badSectors.valid());
        LapRecord impossible = LapCalculator.validate(new Dtos.LapCompleted("s", 0, 1, 5f, 2f, 2f, 1f, true), 1050);
        assertFalse(impossible.valid());
        LapRecord flagged = LapCalculator.validate(new Dtos.LapCompleted("s", 0, 1, 60f, 20f, 20f, 20f, false), 1050);
        assertFalse(flagged.valid());
        assertThrows(ProtocolException.class, () -> LapCalculator.validate(new Dtos.LapCompleted("s", 0, 1, -1f, 0, 0, 0, true), 1050));
    }

    @Test
    void lapStatistics() {
        List<LapRecord> laps = List.of(
                new LapRecord(0, 1, 62, 21, 20, 21, true),
                new LapRecord(0, 2, 60, 20, 20.5, 19.5, true),
                new LapRecord(0, 3, 58, 19, 19, 20, false));
        assertEquals(60, LapCalculator.best(laps).orElseThrow(), 1e-9);
        assertEquals(60, LapCalculator.average(laps).orElseThrow(), 1e-9);
        assertEquals(20 + 20 + 19.5, LapCalculator.theoreticalBest(laps).orElseThrow(), 1e-9);
    }

    @Test
    void standingsUseTimeGapsAtSameDistance() {
        RaceSession s = new RaceSessionManager(600).start(start("gap"));
        // car 0 leads at 50 m/s, car 1 runs exactly 1 s behind
        for (int k = 0; k <= 100; k++) {
            float t = k * 0.1f;
            s.applyTelemetry(telemetry("gap", t, 50f * t, Math.max(0f, 50f * (t - 1f)), 10f, 5f, 1f));
        }
        var st = s.standings();
        assertEquals(0, st.get(0).id());
        assertEquals(1, st.get(1).id());
        assertEquals(1.0, st.get(1).gapToLeader(), 0.12);
        assertEquals(1.0, st.get(1).interval(), 0.12);
    }

    @Test
    void sessionsAreIdempotentOnReconnectAndUnknownSessionsFail() {
        RaceSessionManager m = new RaceSessionManager(600);
        RaceSession a = m.start(start("same"));
        RaceSession b = m.start(start("same"));
        assertSame(a, b);
        assertEquals(5, a.driverCount());
        assertThrows(SessionNotFoundException.class, () -> m.get("missing"));
        assertThrows(ProtocolException.class, () -> m.start(new Dtos.SessionStart("x", "QuickRace", "Easy", "t", 1, 1000, List.of())));
    }

    @Test
    void concurrentTelemetryAndLapsKeepSessionConsistent() throws Exception {
        RaceSession s = new RaceSessionManager(600).start(start("conc"));
        int threads = 8, iterations = 500;
        CountDownLatch done = new CountDownLatch(threads);
        for (int t = 0; t < threads; t++) {
            final int tid = t;
            pool.submit(() -> {
                try {
                    for (int i = 0; i < iterations; i++) {
                        float time = tid * iterations + i;
                        s.applyTelemetry(telemetry("conc", time, time, time - 5, time - 10, time - 15, time - 20));
                        if (i % 100 == 0) {
                            s.recordLap(new Dtos.LapCompleted("conc", tid % 5, i / 100 + 1, 60f, 20f, 20f, 20f, true));
                        }
                        s.snapshot();
                        s.standings();
                    }
                } finally {
                    done.countDown();
                }
            });
        }
        assertTrue(done.await(20, TimeUnit.SECONDS));
        assertEquals(threads * (iterations / 100), s.lapHistory().size());
        assertEquals(5, s.snapshot().size());
    }

    @Test
    void decisionEngineRunsDriversInParallelAndSkipsPlayer() {
        RaceSession s = new RaceSessionManager(600).start(start("ai"));
        s.applyTelemetry(telemetry("ai", 20f, 500f, 490f, 470f, 300f, 488f));
        TrackAnalyzer analyzer = new TrackAnalyzer();
        var analysis = analyzer.analyse(TestTracks.stadium());
        AIDecisionEngine engine = new AIDecisionEngine(pool, 200);
        List<AICommand> cmds = engine.decideAll(s, analysis);
        assertEquals(4, cmds.size(), "one command per AI driver");
        assertTrue(cmds.stream().noneMatch(c -> c.driverId() == 4), "player is never driven");
        // driver 1 is 10 m behind driver 0 and closing → aggressive/defensive logic produces a non-null mode
        cmds.forEach(c -> assertNotNull(c.mode()));
        assertTrue(cmds.stream().anyMatch(c -> c.mode() != TacticalMode.RECOVER));
        assertEquals(4, engine.decisionCount());
    }

    @Test
    void rollingWindowIsBoundedAndAverages() {
        RollingWindow<Integer> w = new RollingWindow<>(3);
        w.add(1);
        w.add(2);
        w.add(3);
        w.add(10);
        assertEquals(3, w.size());
        assertEquals(5.0, w.average(), 1e-9);
        assertEquals(10.0, w.max(), 1e-9);
        assertThrows(IllegalArgumentException.class, () -> new RollingWindow<Double>(0));
    }
}
