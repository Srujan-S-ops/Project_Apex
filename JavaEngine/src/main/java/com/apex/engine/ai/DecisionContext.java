package com.apex.engine.ai;

import com.apex.engine.track.CornerType;

/**
 * Everything a personality needs to decide, computed from an immutable snapshot.
 * Gaps are +∞ when there is no car ahead / behind.
 */
public record DecisionContext(
        int driverId,
        int position,
        int fieldSize,
        double raceTime,
        double speed,
        double lateral,
        double halfWidth,
        int lap,
        int totalLaps,
        double gapAheadMeters,
        double gapAheadSeconds,
        double aheadLateral,
        double aheadSpeed,
        double gapBehindMeters,
        double gapBehindSeconds,
        double behindSpeed,
        int nextCornerDirection,
        double distanceToNextCorner,
        CornerType nextCornerType,
        Difficulty difficulty) {

    public boolean hasCarAhead() {
        return Double.isFinite(gapAheadSeconds);
    }

    public boolean hasCarBehind() {
        return Double.isFinite(gapBehindSeconds);
    }

    public boolean isFinalLap() {
        return totalLaps > 0 && lap >= totalLaps;
    }

    public double closingSpeed() {
        return hasCarAhead() ? speed - aheadSpeed : 0.0;
    }

    public boolean overtakingSpotAhead(double withinMeters) {
        return nextCornerType != null && nextCornerType.isOvertakingSpot() && distanceToNextCorner < withinMeters;
    }
}
