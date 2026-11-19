package com.apex.engine.error;

/** A message referenced a race session the engine does not know (e.g. after an engine restart). */
public class SessionNotFoundException extends ApexException {
    public SessionNotFoundException(String sessionId) {
        super("SESSION_NOT_FOUND", "Unknown race session: " + sessionId);
    }
}
