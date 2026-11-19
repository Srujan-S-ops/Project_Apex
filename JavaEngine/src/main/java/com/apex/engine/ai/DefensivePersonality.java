package com.apex.engine.ai;

/** Position keeper: covers the inside early, only passes at clear opportunities, manages gaps. */
public final class DefensivePersonality extends DriverPersonality {

    public DefensivePersonality(Difficulty difficulty) {
        super(difficulty);
    }

    @Override
    public String name() {
        return "Defensive";
    }

    @Override
    protected double overtakeWindow(DecisionContext c) {
        return c.overtakingSpotAhead(220) ? 0.8 : 0.45;
    }

    @Override
    protected double defendWindow(DecisionContext c) {
        return 1.25;
    }

    @Override
    protected boolean wantsToDefend(DecisionContext c) {
        return true;
    }

    @Override
    protected double overtakeSpeedScale(DecisionContext c) {
        return 1.01;
    }

    @Override
    protected double defensiveLine(DecisionContext c) {
        // tighter cover of the inside line
        return c.nextCornerDirection() * (c.halfWidth() - 1.8) * 0.75;
    }

    @Override
    protected boolean shouldConserve(DecisionContext c) {
        return c.hasCarBehind() && c.gapBehindSeconds() > 2.5 && (!c.hasCarAhead() || c.gapAheadSeconds() > 4.0);
    }

    @Override
    public double aggression() {
        return 0.35;
    }
}
