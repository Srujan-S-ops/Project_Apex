package com.apex.engine;

import com.apex.engine.ai.*;
import com.apex.engine.track.CornerType;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.*;

class PersonalityTest {

    private static DecisionContext ctx(double gapAheadS, double gapBehindS, int position, CornerType nextCorner, double toCorner) {
        double inf = Double.POSITIVE_INFINITY;
        return new DecisionContext(1, position, 5, 60, 50, 0, 6.5, 2, 3,
                Double.isFinite(gapAheadS) ? gapAheadS * 50 : inf, gapAheadS, 1.0, 47,
                Double.isFinite(gapBehindS) ? gapBehindS * 50 : inf, gapBehindS, 52,
                1, toCorner, nextCorner, Difficulty.MEDIUM);
    }

    @Test
    void aggressiveAttacksFromFurtherBackThanDefensive() {
        DecisionContext c = ctx(1.1, Double.POSITIVE_INFINITY, 3, CornerType.MEDIUM, 400);
        assertEquals(TacticalMode.OVERTAKE, new AggressivePersonality(Difficulty.MEDIUM).decide(c).mode());
        assertNotEquals(TacticalMode.OVERTAKE, new DefensivePersonality(Difficulty.MEDIUM).decide(c).mode());
    }

    @Test
    void defensiveCoversWhenAttacked() {
        DecisionContext c = ctx(Double.POSITIVE_INFINITY, 1.0, 4, CornerType.HAIRPIN, 150);
        AICommand d = new DefensivePersonality(Difficulty.MEDIUM).decide(c);
        assertEquals(TacticalMode.DEFEND, d.mode());
        assertTrue(d.lateralTarget() > 0, "covers the inside of a right-hander");
        assertNotEquals(TacticalMode.DEFEND, new AggressivePersonality(Difficulty.MEDIUM).decide(c).mode());
    }

    @Test
    void overtakeGoesToTheInsideAndStaysOnTrack() {
        DecisionContext c = ctx(0.5, Double.POSITIVE_INFINITY, 2, CornerType.HAIRPIN, 120);
        for (DriverPersonality p : new DriverPersonality[]{new AggressivePersonality(Difficulty.HARD), new BalancedPersonality(Difficulty.HARD)}) {
            AICommand cmd = p.decide(c);
            assertEquals(TacticalMode.OVERTAKE, cmd.mode(), p.name());
            assertTrue(cmd.lateralTarget() > c.aheadLateral(), p.name() + " passes on the inside (right)");
            assertTrue(Math.abs(cmd.lateralTarget()) <= c.halfWidth() - 1.3 + 1e-9, p.name() + " target within limits");
            assertTrue(cmd.speedScale() > 1.0);
        }
    }

    @Test
    void leaderWithBigGapConservesUnlessAggressive() {
        DecisionContext c = ctx(Double.POSITIVE_INFINITY, 6.0, 1, CornerType.FAST, 500);
        assertEquals(TacticalMode.CONSERVE, new BalancedPersonality(Difficulty.MEDIUM).decide(c).mode());
        assertEquals(TacticalMode.RACE, new AggressivePersonality(Difficulty.MEDIUM).decide(c).mode());
    }

    @Test
    void stoppedCarRequestsRecovery() {
        double inf = Double.POSITIVE_INFINITY;
        DecisionContext c = new DecisionContext(2, 5, 5, 30, 0.5, 4, 6.5, 2, 3, inf, inf, 0, 0, inf, inf, 0, 1, 100, CornerType.SLOW, Difficulty.EASY);
        assertEquals(TacticalMode.RECOVER, new BalancedPersonality(Difficulty.EASY).decide(c).mode());
    }

    @Test
    void easyDifficultyShrinksOvertakingWindow() {
        // Balanced base window 0.85 s: HARD -> 0.98 s (attacks at 0.9 s), EASY -> 0.60 s (does not)
        DecisionContext c = ctx(0.9, Double.POSITIVE_INFINITY, 3, CornerType.MEDIUM, 400);
        assertEquals(TacticalMode.OVERTAKE, new BalancedPersonality(Difficulty.HARD).decide(withDifficulty(c, Difficulty.HARD)).mode());
        assertNotEquals(TacticalMode.OVERTAKE, new BalancedPersonality(Difficulty.EASY).decide(withDifficulty(c, Difficulty.EASY)).mode());
    }

    private static DecisionContext withDifficulty(DecisionContext c, Difficulty d) {
        return new DecisionContext(c.driverId(), c.position(), c.fieldSize(), c.raceTime(), c.speed(), c.lateral(), c.halfWidth(), c.lap(), c.totalLaps(),
                c.gapAheadMeters(), c.gapAheadSeconds(), c.aheadLateral(), c.aheadSpeed(), c.gapBehindMeters(), c.gapBehindSeconds(), c.behindSpeed(),
                c.nextCornerDirection(), c.distanceToNextCorner(), c.nextCornerType(), d);
    }

    @Test
    void factoryMapsNamesPolymorphically() {
        assertInstanceOf(AggressivePersonality.class, PersonalityFactory.create("Aggressive", Difficulty.MEDIUM));
        assertInstanceOf(DefensivePersonality.class, PersonalityFactory.create("DEFENSIVE", Difficulty.MEDIUM));
        assertInstanceOf(BalancedPersonality.class, PersonalityFactory.create("unknown", Difficulty.MEDIUM));
        DrivingBehaviour b = PersonalityFactory.create(null, Difficulty.MEDIUM);
        assertEquals("Balanced", b.name());
    }
}
