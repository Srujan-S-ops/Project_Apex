package com.apex.engine.error;

/** Root of the engine's exception hierarchy; carries a stable error code sent to clients. */
public class ApexException extends RuntimeException {
    private final String code;

    public ApexException(String code, String message) {
        super(message);
        this.code = code;
    }

    public ApexException(String code, String message, Throwable cause) {
        super(message, cause);
        this.code = code;
    }

    public String getCode() {
        return code;
    }
}
