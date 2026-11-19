package com.apex.engine.track;

import java.util.List;

/** Result of analysing a circuit for a vehicle envelope. */
public record TrackAnalysis(TrackModel model, double[] lineOffset, double[] speed, double[] curvature,
                            List<Corner> corners, List<Integer> brakingZones) {

    public TrackAnalysis {
        corners = List.copyOf(corners);
        brakingZones = List.copyOf(brakingZones);
    }

    /** Next corner at or after sample index {@code index}; null if none. */
    public Corner nextCorner(int index) {
        int n = model.count();
        Corner best = null;
        int bestDist = Integer.MAX_VALUE;
        for (Corner c : corners) {
            int d = ((c.start() - index) % n + n) % n;
            if (d < bestDist) {
                bestDist = d;
                best = c;
            }
        }
        return best;
    }

    public int indexAt(double distance) {
        int n = model.count();
        double d = ((distance % model.length()) + model.length()) % model.length();
        return Math.min(n - 1, (int) (d / model.length() * n));
    }
}
