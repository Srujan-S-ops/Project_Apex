package com.apex.engine.persistence;

import java.time.LocalDateTime;

/** Persistent entities (records map 1:1 to the H2 tables in schema.sql). */
public final class Entities {
    private Entities() {
    }

    public record RaceEntity(long id, String sessionId, String mode, String trackId, String difficulty, String car,
                             int laps, int fieldSize, int playerPosition, Double playerBestLap, Double playerTotal,
                             LocalDateTime createdAt) { }

    public record RaceResultEntity(long id, long raceId, int driverId, String name, boolean isPlayer, int position,
                                   Double totalTime, Double bestLap, int laps, boolean dnf) { }

    public record LapRecordEntity(long id, Long raceId, String sessionId, String trackId, String mode, String driverName,
                                  String car, boolean isPlayer, int lap, double lapTime, Double s1, Double s2, Double s3,
                                  boolean valid, LocalDateTime createdAt) { }

    public record PlayerStatsEntity(String playerId, int races, int wins, int podiums, int totalLaps, Double bestLap) { }
}
