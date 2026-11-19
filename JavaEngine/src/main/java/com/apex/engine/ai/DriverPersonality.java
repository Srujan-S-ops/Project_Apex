package com.apex.engine.ai;

/**
 * Template for AI personalities. The decision flow (recover → overtake → defend → conserve → push
 * → race) is fixed here; subclasses tune it by overriding the hook methods (polymorphism).
 */
public abstract class DriverPersonality implements DrivingBehaviour {
    private final Difficulty difficulty;

    protected DriverPersonality(Difficulty difficulty) {
        this.difficulty = difficulty == null ? Difficulty.MEDIUM : difficulty;
    }

    public Difficulty difficulty() {
        return difficulty;
    }

    @Override
    public final AICommand decide(DecisionContext c) {
        if (c.raceTime() > 8.0 && c.speed() < 2.0) {
            return command(c, TacticalMode.RECOVER, 1.0, c.lateral());
        }
        if (c.hasCarAhead() && c.gapAheadSeconds() < overtakeWindow(c) * difficulty.windowFactor()
                && (c.closingSpeed() > 0.3 || c.gapAheadMeters() < 12.0)) {
            return command(c, TacticalMode.OVERTAKE, overtakeSpeedScale(c) + difficulty.speedOffset(), overtakeLine(c));
        }
        if (c.hasCarBehind() && c.gapBehindSeconds() < defendWindow(c) && wantsToDefend(c)) {
            return command(c, TacticalMode.DEFEND, 1.0 + difficulty.speedOffset(), defensiveLine(c));
        }
        if (shouldConserve(c)) {
            return command(c, TacticalMode.CONSERVE, 0.975, 0.0);
        }
        if (c.hasCarAhead() && c.gapAheadSeconds() < 3.0) {
            return command(c, TacticalMode.PUSH, pushSpeedScale() + difficulty.speedOffset(), 0.0);
        }
        return command(c, TacticalMode.RACE, 1.0 + difficulty.speedOffset(), 0.0);
    }

    protected AICommand command(DecisionContext c, TacticalMode mode, double scale, double lateral) {
        double lim = Math.max(0.0, c.halfWidth() - 1.3);
        return new AICommand(c.driverId(), mode, Math.max(0.9, Math.min(1.06, scale)), Math.max(-lim, Math.min(lim, lateral)), aggression());
    }

    /** Lateral target for a pass: by default the inside of the next corner, beside the car ahead. */
    protected double overtakeLine(DecisionContext c) {
        int side = c.nextCornerDirection() != 0 ? c.nextCornerDirection() : (c.aheadLateral() > 0 ? -1 : 1);
        return c.aheadLateral() + side * 3.0;
    }

    /** Covers the inside of the next corner. */
    protected double defensiveLine(DecisionContext c) {
        return c.nextCornerDirection() * (c.halfWidth() - 2.0) * 0.6;
    }

    protected boolean shouldConserve(DecisionContext c) {
        return c.position() == 1 && c.hasCarBehind() && c.gapBehindSeconds() > 4.0;
    }

    protected double pushSpeedScale() {
        return 1.01;
    }

    /** Seconds behind a rival at which a pass is attempted. */
    protected abstract double overtakeWindow(DecisionContext c);

    /** Seconds ahead of a rival at which defending starts. */
    protected abstract double defendWindow(DecisionContext c);

    protected abstract boolean wantsToDefend(DecisionContext c);

    protected abstract double overtakeSpeedScale(DecisionContext c);

    public abstract double aggression();
}
