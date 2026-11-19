package com.apex.engine.ai;

/** AI difficulty: scales tactical eagerness (pace scaling itself happens in Unity's AIProfile). */
public enum Difficulty {
    EASY(0.7, -0.01), MEDIUM(1.0, 0.0), HARD(1.15, 0.006);

    private final double windowFactor;
    private final double speedOffset;

    Difficulty(double windowFactor, double speedOffset) {
        this.windowFactor = windowFactor;
        this.speedOffset = speedOffset;
    }

    public double windowFactor() {
        return windowFactor;
    }

    public double speedOffset() {
        return speedOffset;
    }

    public static Difficulty parse(String s) {
        if (s == null) {
            return MEDIUM;
        }
        try {
            return valueOf(s.trim().toUpperCase());
        } catch (IllegalArgumentException e) {
            return MEDIUM;
        }
    }
}
