package com.apex.engine.ai;

/** All-rounder: attacks in braking zones, defends when it matters, otherwise races the line. */
public final class BalancedPersonality extends DriverPersonality {

    public BalancedPersonality(Difficulty difficulty) {
        super(difficulty);
    }

    @Override
    public String name() {
        return "Balanced";
    }

    @Override
    protected double overtakeWindow(DecisionContext c) {
        return c.overtakingSpotAhead(220) ? 1.2 : 0.85;
    }

    @Override
    protected double defendWindow(DecisionContext c) {
        return 0.8;
    }

    @Override
    protected boolean wantsToDefend(DecisionContext c) {
        return c.position() <= 3 || c.isFinalLap() || c.overtakingSpotAhead(250);
    }

    @Override
    protected double overtakeSpeedScale(DecisionContext c) {
        return 1.02;
    }

    @Override
    public double aggression() {
        return 0.6;
    }
}
