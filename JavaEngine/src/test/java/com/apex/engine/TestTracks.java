package com.apex.engine;

import com.apex.engine.net.dto.Dtos;

/** Synthetic circuits for tests. */
public final class TestTracks {
    private TestTracks() {
    }

    /** Circle of radius r sampled every ~2 m, clockwise (right-hand corner everywhere). */
    public static Dtos.TrackData circle(double r, float width) {
        int n = (int) Math.round(2 * Math.PI * r / 2.0);
        float[] x = new float[n], y = new float[n], z = new float[n], w = new float[n];
        for (int i = 0; i < n; i++) {
            double a = i / (double) n * Math.PI * 2;
            x[i] = (float) (-Math.cos(a) * r);
            z[i] = (float) (Math.sin(a) * r);
            w[i] = width;
        }
        return new Dtos.TrackData("circle", x, y, z, w, 1.55f, 1300f, 0.5f * 1.225f * 3.1f, 7.5f, 13.5f, 78f);
    }

    /** Stadium: two 400 m straights joined by 40 m-radius hairpins (clockwise). */
    public static Dtos.TrackData stadium() {
        java.util.List<double[]> pts = new java.util.ArrayList<>();
        double r = 40, L = 400;
        for (double s = 0; s < L; s += 2) pts.add(new double[]{s, 0});
        for (double a = 0; a < Math.PI; a += 2 / r) pts.add(new double[]{L + Math.sin(a) * r, -r + Math.cos(a) * r});
        for (double s = L; s > 0; s -= 2) pts.add(new double[]{s, -2 * r});
        for (double a = 0; a < Math.PI; a += 2 / r) pts.add(new double[]{-Math.sin(a) * r, -r - Math.cos(a) * r});
        int n = pts.size();
        float[] x = new float[n], y = new float[n], z = new float[n], w = new float[n];
        for (int i = 0; i < n; i++) {
            x[i] = (float) pts.get(i)[0];
            z[i] = (float) pts.get(i)[1];
            w[i] = 13f;
        }
        return new Dtos.TrackData("stadium", x, y, z, w, 1.55f, 1300f, 0.5f * 1.225f * 3.1f, 7.5f, 13.5f, 78f);
    }
}
