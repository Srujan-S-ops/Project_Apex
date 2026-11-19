package com.apex.engine.race;

import com.apex.engine.ai.DriverPersonality;
import com.apex.engine.util.RollingWindow;

/** Mutable per-driver state inside a RaceSession (guarded by the session's lock). */
public final class DriverState {
    private final int id;
    private final String name;
    private final String car;
    private final boolean player;
    private final DriverPersonality personality;
    private final RollingWindow<Float> speedWindow = new RollingWindow<>(20);

    private double distance;
    private double lapDistance;
    private double lateral;
    private double speed;
    private int lap;
    private int position;
    private double bestLap;
    private int lapsCompleted;

    public DriverState(int id, String name, String car, boolean player, DriverPersonality personality) {
        this.id = id;
        this.name = name;
        this.car = car;
        this.player = player;
        this.personality = personality;
    }

    /** Defensive copy used for lock-free decision making. */
    public DriverState copy() {
        DriverState c = new DriverState(id, name, car, player, personality);
        c.distance = distance;
        c.lapDistance = lapDistance;
        c.lateral = lateral;
        c.speed = speed;
        c.lap = lap;
        c.position = position;
        c.bestLap = bestLap;
        c.lapsCompleted = lapsCompleted;
        return c;
    }

    void update(double distance, double lapDistance, double lateral, double speed, int lap, int position) {
        this.distance = distance;
        this.lapDistance = lapDistance;
        this.lateral = lateral;
        this.speed = speed;
        this.lap = lap;
        this.position = position;
        speedWindow.add((float) speed);
    }

    void onLap(LapRecord r) {
        lapsCompleted = Math.max(lapsCompleted, r.lap());
        if (r.valid() && (bestLap <= 0 || r.lapTime() < bestLap)) {
            bestLap = r.lapTime();
        }
    }

    public int id() { return id; }
    public String name() { return name; }
    public String car() { return car; }
    public boolean isPlayer() { return player; }
    public DriverPersonality personality() { return personality; }
    public double distance() { return distance; }
    public double lapDistance() { return lapDistance; }
    public double lateral() { return lateral; }
    public double speed() { return speed; }
    public double averageSpeed() { return speedWindow.average(); }
    public int lap() { return lap; }
    public int position() { return position; }
    public double bestLap() { return bestLap; }
    public int lapsCompleted() { return lapsCompleted; }
}
