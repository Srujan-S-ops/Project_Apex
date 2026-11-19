package com.apex.engine.ai;

/** Creates personalities from the client's personality names. */
public final class PersonalityFactory {
    private PersonalityFactory() {
    }

    public static DriverPersonality create(String name, Difficulty difficulty) {
        if (name == null) {
            return new BalancedPersonality(difficulty);
        }
        return switch (name.trim().toLowerCase()) {
            case "aggressive" -> new AggressivePersonality(difficulty);
            case "defensive" -> new DefensivePersonality(difficulty);
            default -> new BalancedPersonality(difficulty);
        };
    }
}
