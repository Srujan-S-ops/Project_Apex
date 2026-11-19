package com.apex.engine.track;

/** Corner classification by apex speed. */
public enum CornerType {
    HAIRPIN(0, 22),
    SLOW(22, 33),
    MEDIUM(33, 47),
    FAST(47, 1000);

    private final double minMs;
    private final double maxMs;

    CornerType(double minMs, double maxMs) {
        this.minMs = minMs;
        this.maxMs = maxMs;
    }

    public static CornerType classify(double apexSpeedMs) {
        for (CornerType t : values()) {
            if (apexSpeedMs >= t.minMs && apexSpeedMs < t.maxMs) {
                return t;
            }
        }
        return FAST;
    }

    /** Overtaking opportunity: heavy braking into slow corners. */
    public boolean isOvertakingSpot() {
        return this == HAIRPIN || this == SLOW;
    }
}
