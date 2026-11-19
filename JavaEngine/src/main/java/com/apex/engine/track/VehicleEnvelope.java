package com.apex.engine.track;

/** Performance limits used to turn curvature into target speeds. */
public record VehicleEnvelope(double mu, double massKg, double downforceCoef, double maxAccel, double maxBrake, double topSpeed) {

    public VehicleEnvelope {
        if (mu <= 0 || massKg <= 0 || maxAccel <= 0 || maxBrake <= 0 || topSpeed <= 0 || downforceCoef < 0) {
            throw new IllegalArgumentException("vehicle envelope values must be positive");
        }
    }

    public static VehicleEnvelope gt3() {
        return new VehicleEnvelope(1.55, 1300, 0.5 * 1.225 * 3.1, 7.5, 13.5, 78);
    }
}
