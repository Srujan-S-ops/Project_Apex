package com.apex.engine.track;

/**
 * Maximum-speed profile: lateral-grip limit including downforce (v² k = mu (g + c v² / m)),
 * then a backward braking pass and a forward traction/power pass over the closed loop.
 */
public final class SpeedProfileCalculator {
    private static final double G = 9.81;
    private static final double MAX_DECEL = 32.0;

    public double[] compute(double[] curvature, double ds, VehicleEnvelope e) {
        int n = curvature.length;
        double[] v = new double[n];
        double cm = e.downforceCoef() / e.massKg();
        for (int i = 0; i < n; i++) {
            double k = Math.abs(curvature[i]);
            double denom = k - e.mu() * cm;
            v[i] = denom <= 1e-5 ? e.topSpeed() : Math.min(e.topSpeed(), Math.sqrt(e.mu() * G / denom));
        }
        for (int pass = 0; pass < 2; pass++) {
            for (int j = 0; j < n; j++) {
                int i = ((n - 1 - j) % n + n) % n;
                double vn = v[(i + 1) % n];
                double brake = Math.min(e.maxBrake() + e.mu() * cm * vn * vn, MAX_DECEL);
                double lim = Math.sqrt(vn * vn + 2 * brake * ds);
                if (v[i] > lim) {
                    v[i] = lim;
                }
            }
            for (int i = 0; i < n; i++) {
                int nx = (i + 1) % n;
                double acc = Math.max(0.4, e.maxAccel() * (1 - v[i] / (e.topSpeed() * 1.05)));
                double lim = Math.sqrt(v[i] * v[i] + 2 * acc * ds);
                if (v[nx] > lim) {
                    v[nx] = lim;
                }
            }
        }
        return v;
    }
}
