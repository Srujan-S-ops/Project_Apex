package com.apex.engine.ai;

import com.apex.engine.net.dto.Dtos;

/** Decision for one driver. */
public record AICommand(int driverId, TacticalMode mode, double speedScale, double lateralTarget, double aggression) {

    public static AICommand neutral(int driverId) {
        return new AICommand(driverId, TacticalMode.RACE, 1.0, 0.0, 0.5);
    }

    public Dtos.AICommandDto toDto() {
        return new Dtos.AICommandDto(driverId, mode.name(), (float) speedScale, (float) lateralTarget, (float) aggression);
    }
}
