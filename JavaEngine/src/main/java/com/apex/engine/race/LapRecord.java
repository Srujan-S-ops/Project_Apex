package com.apex.engine.race;

/** A completed, validated lap. */
public record LapRecord(int driverId, int lap, double lapTime, double s1, double s2, double s3, boolean valid) {
}
