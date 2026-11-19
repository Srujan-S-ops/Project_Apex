package com.apex.engine.track;

import com.apex.engine.error.TrackAnalysisException;

/**
 * Immutable closed centre line sampled at uniform arc length, with per-sample width and the
 * horizontal right-hand normal. Encapsulates all geometry maths used by the analysers.
 */
public final class TrackModel {
    private final String trackId;
    private final double[] x, y, z, width;
    private final double[] rightX, rightZ;
    private final double length;
    private final int count;

    public TrackModel(String trackId, float[] x, float[] y, float[] z, float[] width) {
        if (x == null || y == null || z == null || width == null) {
            throw new TrackAnalysisException("track arrays missing");
        }
        int n = x.length;
        if (n < 32 || y.length != n || z.length != n || width.length != n) {
            throw new TrackAnalysisException("track arrays must have equal length >= 32 (got " + n + ")");
        }
        this.trackId = trackId == null ? "unknown" : trackId;
        this.count = n;
        this.x = new double[n];
        this.y = new double[n];
        this.z = new double[n];
        this.width = new double[n];
        for (int i = 0; i < n; i++) {
            if (!Float.isFinite(x[i]) || !Float.isFinite(y[i]) || !Float.isFinite(z[i]) || !Float.isFinite(width[i]) || width[i] <= 2f) {
                throw new TrackAnalysisException("invalid sample at index " + i);
            }
            this.x[i] = x[i];
            this.y[i] = y[i];
            this.z[i] = z[i];
            this.width[i] = width[i];
        }
        double len = 0;
        for (int i = 0; i < n; i++) {
            int j = (i + 1) % n;
            len += Math.sqrt(sq(this.x[j] - this.x[i]) + sq(this.y[j] - this.y[i]) + sq(this.z[j] - this.z[i]));
        }
        if (len < 200) {
            throw new TrackAnalysisException("degenerate track (length " + len + " m)");
        }
        this.length = len;
        this.rightX = new double[n];
        this.rightZ = new double[n];
        for (int i = 0; i < n; i++) {
            double fx = this.x[(i + 1) % n] - this.x[(i - 1 + n) % n];
            double fz = this.z[(i + 1) % n] - this.z[(i - 1 + n) % n];
            double l = Math.hypot(fx, fz);
            if (l < 1e-9) {
                throw new TrackAnalysisException("duplicate samples around index " + i);
            }
            rightX[i] = fz / l;
            rightZ[i] = -fx / l;
        }
    }

    private static double sq(double v) {
        return v * v;
    }

    public String trackId() {
        return trackId;
    }

    public int count() {
        return count;
    }

    public double length() {
        return length;
    }

    public double spacing() {
        return length / count;
    }

    public double width(int i) {
        return width[wrap(i)];
    }

    public int wrap(int i) {
        return ((i % count) + count) % count;
    }

    /** Horizontal position (x, z) of the point offset laterally by {@code lateral} metres. */
    public double[] point(int i, double lateral) {
        int k = wrap(i);
        return new double[]{x[k] + rightX[k] * lateral, z[k] + rightZ[k] * lateral};
    }

    public double rightX(int i) {
        return rightX[wrap(i)];
    }

    public double rightZ(int i) {
        return rightZ[wrap(i)];
    }

    public double centreX(int i) {
        return x[wrap(i)];
    }

    public double centreZ(int i) {
        return z[wrap(i)];
    }

    /** Signed curvature through points i-k, i, i+k of the given polyline (+ = right turn). */
    public static double signedCurvature(double[][] pts, int i, int k) {
        int n = pts.length;
        double[] a = pts[((i - k) % n + n) % n], b = pts[i], c = pts[(i + k) % n];
        double ab = Math.hypot(b[0] - a[0], b[1] - a[1]);
        double bc = Math.hypot(c[0] - b[0], c[1] - b[1]);
        double ca = Math.hypot(a[0] - c[0], a[1] - c[1]);
        double cross = (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0]);
        double denom = ab * bc * ca;
        return denom < 1e-9 ? 0.0 : -2.0 * cross / denom;
    }
}
