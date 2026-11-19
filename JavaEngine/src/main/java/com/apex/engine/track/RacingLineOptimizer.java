package com.apex.engine.track;

/**
 * Approximates the minimum-curvature racing line by iterative Laplacian relaxation of lateral
 * offsets, constrained to the track width minus a safety margin.
 */
public final class RacingLineOptimizer {
    private final double margin;
    private final int iterations;
    private final int neighbour;

    public RacingLineOptimizer(double margin, int iterations, int neighbour) {
        this.margin = margin;
        this.iterations = iterations;
        this.neighbour = neighbour;
    }

    public RacingLineOptimizer() {
        this(1.9, 400, 3);
    }

    public double[] optimise(TrackModel t) {
        int n = t.count();
        double[] off = new double[n];
        double[] next = new double[n];
        double[][] pts = new double[n][];
        for (int it = 0; it < iterations; it++) {
            for (int i = 0; i < n; i++) {
                pts[i] = t.point(i, off[i]);
            }
            for (int i = 0; i < n; i++) {
                double[] a = pts[t.wrap(i - neighbour)], b = pts[t.wrap(i + neighbour)];
                double tx = (a[0] + b[0]) * 0.5 - t.centreX(i);
                double tz = (a[1] + b[1]) * 0.5 - t.centreZ(i);
                double o = tx * t.rightX(i) + tz * t.rightZ(i);
                double lim = t.width(i) * 0.5 - margin;
                double v = off[i] + (o - off[i]) * 0.6;
                next[i] = Math.max(-lim, Math.min(lim, v));
            }
            System.arraycopy(next, 0, off, 0, n);
        }
        return off;
    }

    /** Smoothed signed curvature of the racing line defined by the offsets. */
    public double[] curvature(TrackModel t, double[] offsets) {
        int n = t.count();
        double[][] pts = new double[n][];
        for (int i = 0; i < n; i++) {
            pts[i] = t.point(i, offsets[i]);
        }
        double[] c = new double[n];
        for (int i = 0; i < n; i++) {
            c[i] = TrackModel.signedCurvature(pts, i, 5);
        }
        double[] tmp = new double[n];
        for (int pass = 0; pass < 2; pass++) {
            for (int i = 0; i < n; i++) {
                tmp[i] = (c[t.wrap(i - 1)] + 2 * c[i] + c[t.wrap(i + 1)]) * 0.25;
            }
            System.arraycopy(tmp, 0, c, 0, n);
        }
        return c;
    }
}
