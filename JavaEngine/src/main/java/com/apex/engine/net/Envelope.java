package com.apex.engine.net;

/** Flat message envelope: the payload is a JSON string so Unity's JsonUtility can parse it. */
public record Envelope(String type, long seq, String payload) {
}
