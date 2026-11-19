package com.apex.engine.net;

import com.apex.engine.race.RaceSessionManager;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Component;

/** Closes silent connections (client crashed / network gone) and evicts abandoned race sessions. */
@Component
public class HeartbeatMonitor {
    private static final Logger LOG = LoggerFactory.getLogger(HeartbeatMonitor.class);
    private final ConnectionRegistry registry;
    private final RaceSessionManager sessions;
    private final long timeoutMs;

    public HeartbeatMonitor(ConnectionRegistry registry, RaceSessionManager sessions,
                            @Value("${apex.connection.heartbeat-timeout-seconds:15}") long timeoutSeconds) {
        this.registry = registry;
        this.sessions = sessions;
        this.timeoutMs = timeoutSeconds * 1000L;
    }

    @Scheduled(fixedDelay = 5000)
    public void sweep() {
        for (ClientConnection c : registry.all()) {
            if (c.idleMillis() > timeoutMs) {
                LOG.warn("Connection {} timed out after {} ms of silence", c.id(), c.idleMillis());
                c.close();
                registry.remove(c.id());
            }
        }
        int evicted = sessions.evictIdle();
        if (evicted > 0) {
            LOG.info("Evicted {} idle race sessions", evicted);
        }
    }
}
