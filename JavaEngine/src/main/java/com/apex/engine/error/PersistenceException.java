package com.apex.engine.error;

/** Database access failure wrapped with an engine error code. */
public class PersistenceException extends ApexException {
    public PersistenceException(String message, Throwable cause) {
        super("PERSISTENCE", message, cause);
    }
}
