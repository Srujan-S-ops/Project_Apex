package com.apex.engine.error;

/** Malformed, unknown or semantically invalid client message. */
public class ProtocolException extends ApexException {
    public ProtocolException(String message) {
        super("PROTOCOL", message);
    }

    public ProtocolException(String message, Throwable cause) {
        super("PROTOCOL", message, cause);
    }
}
