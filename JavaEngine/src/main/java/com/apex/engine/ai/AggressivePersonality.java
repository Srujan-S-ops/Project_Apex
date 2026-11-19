package com.apex.engine.ai;

/** Late-braking attacker: long overtaking window, dives down the inside, rarely defends early. */
public final class AggressivePersonality extends DriverPersonality {

    public AggressivePersonality(Difficulty difficulty) {
        super(difficulty);
    }

    @Override
    public String name() {
        return "Aggressive";
    }

    @Override
    protected double overtakeWindow(DecisionContext c) {
        return c.overtakingSpotAhead(250) ? 1.6 : 1.25;
    }

    @Override
    protected double defendWindow(DecisionContext c) {
        return 0.5;
    }

    @Override
    protected boolean wantsToDefend(DecisionContext c) {
        return c.isFinalLap() || c.position() <= 2;
    }

    @Override
    protected double overtakeSpeedScale(DecisionContext c) {
        return c.overtakingSpotAhead(200) ? 1.04 : 1.03;
    }

    @Override
    protected double overtakeLine(DecisionContext c) {
        // always commits to the inside of the next corner
        int side = c.nextCornerDirection() != 0 ? c.nextCornerDirection() : 1;
        return c.aheadLateral() + side * 3.1;
    }

    @Override
    protected boolean shouldConserve(DecisionContext c) {
        return false;   // never lifts while leading
    }

    @Override
    protected double pushSpeedScale() {
        return 1.02;
    }

    @Override
    public double aggression() {
        return 0.9;
    }
}
