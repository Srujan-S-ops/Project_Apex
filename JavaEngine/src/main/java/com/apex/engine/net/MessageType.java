package com.apex.engine.net;

import java.util.Arrays;
import java.util.Optional;

/** Message types of the Unity / engine protocol. */
public enum MessageType {
    HELLO("hello"), WELCOME("welcome"), PING("ping"), PONG("pong"),
    TRACK_DATA("trackData"), TRACK_ANALYSIS("trackAnalysis"),
    SESSION_START("sessionStart"), TELEMETRY("telemetry"), AI_COMMANDS("aiCommands"), STANDINGS("standings"),
    LAP_COMPLETED("lapCompleted"), RACE_FINISHED("raceFinished"), RACE_SAVED("raceSaved"),
    GET_PROFILE("getProfile"), PROFILE("profile"), ERROR("error");

    private final String wire;

    MessageType(String wire) {
        this.wire = wire;
    }

    public String wire() {
        return wire;
    }

    public static Optional<MessageType> fromWire(String s) {
        return Arrays.stream(values()).filter(t -> t.wire.equals(s)).findFirst();
    }
}
