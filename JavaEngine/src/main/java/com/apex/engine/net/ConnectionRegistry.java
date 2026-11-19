package com.apex.engine.net;

import org.springframework.stereotype.Component;

import java.util.Collection;
import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;

/** Live client connections keyed by WebSocket session id. */
@Component
public class ConnectionRegistry {
    private final Map<String, ClientConnection> connections = new ConcurrentHashMap<>();

    public void add(ClientConnection c) {
        connections.put(c.id(), c);
    }

    public ClientConnection get(String id) {
        return connections.get(id);
    }

    public ClientConnection remove(String id) {
        return connections.remove(id);
    }

    public Collection<ClientConnection> all() {
        return connections.values();
    }

    public int size() {
        return connections.size();
    }
}
