package com.apex.engine;

import com.apex.engine.error.TrackAnalysisException;
import com.apex.engine.net.dto.Dtos;
import com.apex.engine.track.CornerType;
import com.apex.engine.track.TrackAnalysis;
import com.apex.engine.track.TrackAnalyzer;
import com.apex.engine.track.VehicleEnvelope;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.*;

class TrackAnalyzerTest {
    private final TrackAnalyzer analyzer = new TrackAnalyzer();

    @Test
    void circleSpeedMatchesGripFormulaIncludingDownforce() {
        double r = 60;
        TrackAnalysis a = analyzer.analyse(TestTracks.circle(r, 12f));
        VehicleEnvelope e = VehicleEnvelope.gt3();
        // the racing line hugs the inside: radius ≈ r - (w/2 - margin)
        double rLine = r - (6 - 1.9);
        double k = 1 / rLine;
        double expected = Math.sqrt(e.mu() * 9.81 / (k - e.mu() * e.downforceCoef() / e.massKg()));
        double mid = a.speed()[a.speed().length / 2];
        assertEquals(expected, mid, expected * 0.06, "steady-state corner speed");
    }

    @Test
    void racingLineRespectsTrackLimits() {
        TrackAnalysis a = analyzer.analyse(TestTracks.stadium());
        for (int i = 0; i < a.lineOffset().length; i++) {
            assertTrue(Math.abs(a.lineOffset()[i]) <= 13 / 2.0 - 1.9 + 1e-6, "offset within limits at " + i);
        }
    }

    @Test
    void stadiumHasTwoHairpinsAndBrakingZones() {
        TrackAnalysis a = analyzer.analyse(TestTracks.stadium());
        assertEquals(2, a.corners().size());
        a.corners().forEach(c -> assertTrue(c.type() == CornerType.HAIRPIN || c.type() == CornerType.SLOW, c.toString()));
        assertTrue(a.corners().stream().allMatch(c -> c.direction() == 1), "clockwise stadium turns right");
        assertEquals(2, a.brakingZones().size());
        double maxSpeed = java.util.Arrays.stream(a.speed()).max().orElse(0);
        double minSpeed = java.util.Arrays.stream(a.speed()).min().orElse(0);
        assertTrue(maxSpeed > minSpeed + 20, "straights must be much faster than hairpins");
    }

    @Test
    void speedProfileNeverExceedsBrakingCapability() {
        TrackAnalysis a = analyzer.analyse(TestTracks.stadium());
        double ds = a.model().spacing();
        double[] v = a.speed();
        for (int i = 0; i < v.length; i++) {
            double next = v[(i + 1) % v.length];
            double decel = (v[i] * v[i] - next * next) / (2 * ds);
            assertTrue(decel <= 32.0 + 1e-6, "decel " + decel + " at " + i);
        }
    }

    @Test
    void analysisIsCachedForIdenticalGeometry() {
        Dtos.TrackData t = TestTracks.stadium();
        assertSame(analyzer.analyse(t), analyzer.analyse(t));
        assertNotNull(analyzer.forTrack("stadium"));
    }

    @Test
    void rejectsDegenerateInput() {
        float[] small = new float[10];
        assertThrows(TrackAnalysisException.class,
                () -> analyzer.analyse(new Dtos.TrackData("bad", small, small, small, small, 1.5f, 1300, 3, 7, 13, 78)));
        Dtos.TrackData t = TestTracks.circle(50, 12);
        t.x()[5] = Float.NaN;
        assertThrows(TrackAnalysisException.class, () -> analyzer.analyse(t));
    }

    @Test
    void dtoArraysMatchSampleCount() {
        Dtos.TrackData t = TestTracks.stadium();
        var dto = analyzer.toDto(analyzer.analyse(t));
        assertEquals(t.x().length, dto.lineOffset().length);
        assertEquals(t.x().length, dto.speed().length);
        assertEquals("stadium", dto.trackId());
    }
}
