package com.apex.engine.race;

import com.apex.engine.error.ProtocolException;
import com.apex.engine.error.SessionNotFoundException;
import com.apex.engine.net.dto.Dtos;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Service;

import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;

/** Registry of active race sessions (ConcurrentHashMap, safe for concurrent clients). */
@Service
public class RaceSessionManager {
    private static final Logger LOG = LoggerFactory.getLogger(RaceSessionManager.class);
    private final Map<String, RaceSession> sessions = new ConcurrentHashMap<>();
    private final long idleTimeoutMs;

    public RaceSessionManager(@Value("${apex.session.idle-timeout-seconds:600}") long idleSeconds) {
        this.idleTimeoutMs = idleSeconds * 1000L;
    }

    /** Creates the session, or re-attaches to it after a client reconnection (idempotent). */
    public RaceSession start(Dtos.SessionStart s) {
        if (s == null || s.sessionId() == null || s.sessionId().isBlank()) {
            throw new ProtocolException("sessionStart requires a sessionId");
        }
        if (s.drivers() == null || s.drivers().isEmpty()) {
            throw new ProtocolException("sessionStart requires at least one driver");
        }
        RaceSession session = sessions.compute(s.sessionId(), (id, existing) -> {
            if (existing != null) {
                existing.registerDrivers(s.drivers());
                LOG.info("Session {} re-attached ({} drivers)", id, existing.driverCount());
                return existing;
            }
            LOG.info("Session {} started: {} {} laps={} drivers={}", id, s.mode(), s.difficulty(), s.laps(), s.drivers().size());
            return new RaceSession(s);
        });
        return session;
    }

    public RaceSession get(String id) {
        RaceSession s = id == null ? null : sessions.get(id);
        if (s == null) {
            throw new SessionNotFoundException(id);
        }
        return s;
    }

    public RaceSession find(String id) {
        return id == null ? null : sessions.get(id);
    }

    public int evictIdle() {
        long now = System.currentTimeMillis();
        int before = sessions.size();
        sessions.values().removeIf(s -> now - s.lastActivity() > idleTimeoutMs);
        return before - sessions.size();
    }

    public int size() {
        return sessions.size();
    }
}
