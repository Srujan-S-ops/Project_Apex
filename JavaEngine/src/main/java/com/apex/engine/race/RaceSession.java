package com.apex.engine.race;

import com.apex.engine.ai.Difficulty;
import com.apex.engine.ai.PersonalityFactory;
import com.apex.engine.net.dto.Dtos;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;

/**
 * Server-side model of one race / time-trial session. Telemetry, lap events and decision snapshots
 * may arrive on different threads, so all mutation is synchronized on this instance.
 */
public final class RaceSession {
    public enum Status { RUNNING, FINISHED }

    private final String id;
    private final String mode;
    private final Difficulty difficulty;
    private final String trackId;
    private final int laps;
    private final double trackLength;
    private final Map<Integer, DriverState> drivers = new ConcurrentHashMap<>();
    private final List<LapRecord> lapHistory = Collections.synchronizedList(new ArrayList<>());
    private final Leaderboard leaderboard = new Leaderboard();
    private volatile long lastActivity = System.currentTimeMillis();
    private volatile double raceTime;
    private volatile Status status = Status.RUNNING;

    public RaceSession(Dtos.SessionStart s) {
        this.id = s.sessionId();
        this.mode = s.mode() == null ? "QuickRace" : s.mode();
        this.difficulty = Difficulty.parse(s.difficulty());
        this.trackId = s.trackId();
        this.laps = s.laps();
        this.trackLength = s.trackLength();
        registerDrivers(s.drivers());
    }

    /** (Re-)registers drivers; called on creation and when a client reconnects. */
    public synchronized void registerDrivers(List<Dtos.DriverDto> list) {
        if (list == null) {
            return;
        }
        for (Dtos.DriverDto d : list) {
            drivers.computeIfAbsent(d.id(), k -> new DriverState(d.id(), d.name(), d.car(), d.isPlayer(),
                    d.isPlayer() ? null : PersonalityFactory.create(d.personality(), difficulty)));
        }
        touch();
    }

    public synchronized void applyTelemetry(Dtos.Telemetry t) {
        raceTime = t.raceTime();
        if (t.cars() != null) {
            for (Dtos.CarTelemetry c : t.cars()) {
                DriverState d = drivers.get(c.id());
                if (d == null) {
                    continue;
                }
                d.update(c.distance(), c.lapDistance(), c.lateral(), c.speed(), c.lap(), c.position());
                leaderboard.record(c.id(), c.distance(), t.raceTime());
            }
        }
        touch();
    }

    public synchronized LapRecord recordLap(Dtos.LapCompleted m) {
        LapRecord r = LapCalculator.validate(m, trackLength);
        DriverState d = drivers.get(r.driverId());
        if (d != null) {
            d.onLap(r);
        }
        lapHistory.add(r);
        touch();
        return r;
    }

    /** Consistent copy of all drivers for lock-free AI decisions on worker threads. */
    public synchronized List<DriverState> snapshot() {
        List<DriverState> copy = new ArrayList<>(drivers.size());
        for (DriverState d : drivers.values()) {
            copy.add(d.copy());
        }
        return copy;
    }

    public synchronized List<Dtos.StandingDto> standings() {
        return leaderboard.standings(drivers.values(), raceTime);
    }

    public List<LapRecord> lapHistory() {
        synchronized (lapHistory) {
            return List.copyOf(lapHistory);
        }
    }

    public DriverState driver(int id) {
        return drivers.get(id);
    }

    public void finish() {
        status = Status.FINISHED;
        touch();
    }

    private void touch() {
        lastActivity = System.currentTimeMillis();
    }

    public String id() { return id; }
    public String mode() { return mode; }
    public Difficulty difficulty() { return difficulty; }
    public String trackId() { return trackId; }
    public int laps() { return laps; }
    public double trackLength() { return trackLength; }
    public double raceTime() { return raceTime; }
    public long lastActivity() { return lastActivity; }
    public Status status() { return status; }
    public int driverCount() { return drivers.size(); }
}
