package com.apex.engine.net.dto;

import java.util.List;

/**
 * Wire DTOs shared with the Unity client (Apex.Net.Protocol). Component names are the contract.
 */
public final class Dtos {
    private Dtos() {
    }

    public record Hello(String clientVersion) { }

    public record Welcome(String serverVersion, String connectionId) { }

    public record Ping(double t) { }

    public record TrackData(String trackId, float[] x, float[] y, float[] z, float[] width,
                            float mu, float massKg, float downforceCoef, float maxAccel, float maxBrake, float topSpeed) { }

    public record CornerDto(int start, int apex, int end, float minSpeed, String type) { }

    public record TrackAnalysisDto(String trackId, float[] lineOffset, float[] speed, float[] curvature,
                                   List<CornerDto> corners, int[] brakingZones) { }

    public record DriverDto(int id, String name, String personality, boolean isPlayer, String car) { }

    public record SessionStart(String sessionId, String mode, String difficulty, String trackId, int laps,
                               float trackLength, List<DriverDto> drivers) { }

    public record CarTelemetry(int id, float distance, float lapDistance, float lateral, float speed, int lap, int position) { }

    public record Telemetry(String sessionId, float raceTime, List<CarTelemetry> cars) { }

    public record AICommandDto(int id, String mode, float speedScale, float lateralTarget, float aggression) { }

    public record AICommands(String sessionId, List<AICommandDto> commands) { }

    public record StandingDto(int id, int position, int lap, float gapToLeader, float interval, float bestLap) { }

    public record Standings(String sessionId, List<StandingDto> entries) { }

    public record LapCompleted(String sessionId, int driverId, int lap, float lapTime, float s1, float s2, float s3, boolean valid) { }

    public record ResultDto(int driverId, String name, boolean isPlayer, int position, float totalTime, float bestLap, int laps, boolean dnf) { }

    public record RaceFinished(String sessionId, String mode, String trackId, String difficulty, String car, int laps,
                               List<ResultDto> results) { }

    public record RaceSaved(long raceId, boolean newPersonalBest, float personalBest) { }

    public record GetProfile(String trackId) { }

    public record RecentRaceDto(long raceId, String date, String mode, String trackId, String difficulty,
                                int position, int fieldSize, float bestLap, float totalTime) { }

    public record LapRecordDto(String driver, String car, String mode, String date, float lapTime, boolean isPlayer) { }

    public record Profile(int races, int wins, int podiums, int totalLaps, float bestLap,
                          List<RecentRaceDto> recent, List<LapRecordDto> records) { }

    public record ErrorDto(String code, String message) { }
}
