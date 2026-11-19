package com.apex.engine.track;

import com.apex.engine.error.TrackAnalysisException;
import com.apex.engine.net.dto.Dtos;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;
import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;

/**
 * Analyses circuits sent by the client: racing line, speed profile, corners and braking zones.
 * Results are cached per track geometry (ConcurrentHashMap) because clients resend the track
 * after every reconnection.
 */
@Service
public class TrackAnalyzer {
    private static final Logger LOG = LoggerFactory.getLogger(TrackAnalyzer.class);
    private static final double CORNER_THRESHOLD = 1.0 / 160.0;

    private final RacingLineOptimizer lineOptimizer = new RacingLineOptimizer();
    private final SpeedProfileCalculator speedCalculator = new SpeedProfileCalculator();
    private final Map<String, TrackAnalysis> cache = new ConcurrentHashMap<>();

    public TrackAnalysis analyse(Dtos.TrackData data) {
        if (data == null) {
            throw new TrackAnalysisException("no track data");
        }
        String key = data.trackId() + ":" + Arrays.hashCode(data.x()) + ":" + Arrays.hashCode(data.z()) + ":" + data.mu();
        return cache.computeIfAbsent(key, k -> compute(data));
    }

    /** Latest analysis for a track id (any envelope), if present. */
    public TrackAnalysis forTrack(String trackId) {
        return cache.entrySet().stream()
                .filter(e -> e.getKey().startsWith(trackId + ":"))
                .map(Map.Entry::getValue)
                .findFirst().orElse(null);
    }

    private TrackAnalysis compute(Dtos.TrackData d) {
        long t0 = System.nanoTime();
        TrackModel model = new TrackModel(d.trackId(), d.x(), d.y(), d.z(), d.width());
        VehicleEnvelope env;
        try {
            env = new VehicleEnvelope(d.mu(), d.massKg(), d.downforceCoef(), d.maxAccel(), d.maxBrake(), d.topSpeed());
        } catch (IllegalArgumentException e) {
            LOG.warn("Invalid vehicle envelope from client ({}); using GT3 defaults", e.getMessage());
            env = VehicleEnvelope.gt3();
        }
        double[] offsets = lineOptimizer.optimise(model);
        double[] curvature = lineOptimizer.curvature(model, offsets);
        double[] speed = speedCalculator.compute(curvature, model.spacing(), env);
        List<Corner> corners = extractCorners(model, curvature, speed);
        List<Integer> braking = brakingZones(speed);
        LOG.info("Analysed track '{}' ({} samples, {} m): {} corners, {} braking zones in {} ms",
                model.trackId(), model.count(), Math.round(model.length()), corners.size(), braking.size(),
                (System.nanoTime() - t0) / 1_000_000);
        return new TrackAnalysis(model, offsets, speed, curvature, corners, braking);
    }

    static List<Corner> extractCorners(TrackModel model, double[] curvature, double[] speed) {
        int n = model.count();
        int start = 0;
        for (int i = 0; i < n; i++) {
            if (Math.abs(curvature[i]) < CORNER_THRESHOLD * 0.5) {
                start = i;
                break;
            }
        }
        List<Corner> out = new ArrayList<>();
        boolean in = false;
        int cs = 0, apex = 0;
        double minSpeed = Double.MAX_VALUE;
        for (int j = 0; j <= n; j++) {
            int i = (start + j) % n;
            double k = Math.abs(curvature[i]);
            if (!in && k > CORNER_THRESHOLD) {
                in = true;
                cs = i;
                apex = i;
                minSpeed = speed[i];
            } else if (in) {
                if (k > Math.abs(curvature[apex])) {
                    apex = i;
                }
                minSpeed = Math.min(minSpeed, speed[i]);
                if (k < CORNER_THRESHOLD * 0.7) {
                    in = false;
                    int len = ((i - cs) % n + n) % n;
                    if (len >= 3) {
                        out.add(new Corner(cs, apex, i, minSpeed, curvature[apex] > 0 ? 1 : -1, CornerType.classify(minSpeed)));
                    }
                }
            }
        }
        return out;
    }

    static List<Integer> brakingZones(double[] speed) {
        int n = speed.length;
        List<Integer> zones = new ArrayList<>();
        for (int i = 0; i < n; i++) {
            // local maximum followed by deceleration = the braking point
            boolean startsDecel = speed[(i + 1) % n] < speed[i] - 0.05 && speed[(i - 1 + n) % n] <= speed[i] + 0.05;
            if (startsDecel && speed[i] > speed[(i + 30) % n] + 8) {
                zones.add(i);
            }
        }
        return zones;
    }

    public Dtos.TrackAnalysisDto toDto(TrackAnalysis a) {
        int n = a.model().count();
        float[] off = new float[n], spd = new float[n], cur = new float[n];
        for (int i = 0; i < n; i++) {
            off[i] = (float) a.lineOffset()[i];
            spd[i] = (float) a.speed()[i];
            cur[i] = (float) a.curvature()[i];
        }
        List<Dtos.CornerDto> corners = a.corners().stream()
                .map(c -> new Dtos.CornerDto(c.start(), c.apex(), c.end(), (float) c.minSpeed(), c.type().name()))
                .toList();
        int[] braking = a.brakingZones().stream().mapToInt(Integer::intValue).toArray();
        return new Dtos.TrackAnalysisDto(a.model().trackId(), off, spd, cur, corners, braking);
    }
}
