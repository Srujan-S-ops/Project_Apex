package com.apex.engine.net;

import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.web.socket.TextMessage;
import org.springframework.web.socket.WebSocketSession;
import org.springframework.web.socket.handler.ConcurrentWebSocketSessionDecorator;

import java.io.IOException;
import java.util.concurrent.atomic.AtomicLong;

/** One connected Unity client. Sends are thread-safe (decorated session with bounded buffer). */
public final class ClientConnection {
    private static final Logger LOG = LoggerFactory.getLogger(ClientConnection.class);

    private final WebSocketSession session;
    private final ObjectMapper mapper;
    private final AtomicLong seq = new AtomicLong();
    private volatile long lastSeenMillis = System.currentTimeMillis();
    private volatile String raceSessionId;
    private volatile long lastStandingsMillis;

    public ClientConnection(WebSocketSession raw, ObjectMapper mapper) {
        this.session = new ConcurrentWebSocketSessionDecorator(raw, 5_000, 2 * 1024 * 1024);
        this.mapper = mapper;
    }

    public String id() {
        return session.getId();
    }

    public boolean isOpen() {
        return session.isOpen();
    }

    public void touch() {
        lastSeenMillis = System.currentTimeMillis();
    }

    public long idleMillis() {
        return System.currentTimeMillis() - lastSeenMillis;
    }

    public String raceSessionId() {
        return raceSessionId;
    }

    public void bindRaceSession(String id) {
        this.raceSessionId = id;
    }

    /** Returns true at most every intervalMs (used to throttle standings broadcasts). */
    public boolean standingsDue(long intervalMs) {
        long now = System.currentTimeMillis();
        if (now - lastStandingsMillis >= intervalMs) {
            lastStandingsMillis = now;
            return true;
        }
        return false;
    }

    public void send(MessageType type, Object payload) {
        if (!session.isOpen()) {
            return;
        }
        try {
            String inner = mapper.writeValueAsString(payload);
            String json = mapper.writeValueAsString(new Envelope(type.wire(), seq.incrementAndGet(), inner));
            session.sendMessage(new TextMessage(json));
        } catch (JsonProcessingException e) {
            LOG.error("Serialisation failed for {}", type, e);
        } catch (IOException | IllegalStateException e) {
            LOG.warn("Send to {} failed ({}): {}", id(), type, e.getMessage());
        }
    }

    public void close() {
        try {
            session.close();
        } catch (IOException e) {
            LOG.debug("close failed", e);
        }
    }
}
