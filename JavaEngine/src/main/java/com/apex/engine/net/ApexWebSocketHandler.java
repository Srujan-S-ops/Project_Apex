package com.apex.engine.net;

import com.apex.engine.error.ApexException;
import com.apex.engine.error.ProtocolException;
import com.apex.engine.net.dto.Dtos;
import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Component;
import org.springframework.web.socket.CloseStatus;
import org.springframework.web.socket.TextMessage;
import org.springframework.web.socket.WebSocketSession;
import org.springframework.web.socket.handler.TextWebSocketHandler;

/**
 * WebSocket endpoint. Decodes envelopes, routes them and converts every failure into an
 * {@code error} message instead of dropping the connection.
 */
@Component
public class ApexWebSocketHandler extends TextWebSocketHandler {
    private static final Logger LOG = LoggerFactory.getLogger(ApexWebSocketHandler.class);
    private final ObjectMapper mapper;
    private final ConnectionRegistry registry;
    private final MessageRouter router;

    public ApexWebSocketHandler(ObjectMapper mapper, ConnectionRegistry registry, MessageRouter router) {
        this.mapper = mapper;
        this.registry = registry;
        this.router = router;
    }

    @Override
    public void afterConnectionEstablished(WebSocketSession session) {
        session.setTextMessageSizeLimit(4 * 1024 * 1024);
        registry.add(new ClientConnection(session, mapper));
        LOG.info("Client connected: {} ({} active)", session.getId(), registry.size());
    }

    @Override
    protected void handleTextMessage(WebSocketSession session, TextMessage message) {
        ClientConnection c = registry.get(session.getId());
        if (c == null) {
            return;
        }
        c.touch();
        try {
            Envelope env = decode(message.getPayload());
            router.route(c, env);
        } catch (ApexException e) {
            LOG.warn("Rejected message from {}: [{}] {}", c.id(), e.getCode(), e.getMessage());
            c.send(MessageType.ERROR, new Dtos.ErrorDto(e.getCode(), e.getMessage()));
        } catch (RuntimeException e) {
            LOG.error("Unexpected failure handling message from {}", c.id(), e);
            c.send(MessageType.ERROR, new Dtos.ErrorDto("INTERNAL", "internal error: " + e.getClass().getSimpleName()));
        }
    }

    private Envelope decode(String raw) {
        try {
            Envelope env = mapper.readValue(raw, Envelope.class);
            if (env == null || env.type() == null || env.type().isBlank()) {
                throw new ProtocolException("envelope without type");
            }
            return env;
        } catch (JsonProcessingException e) {
            throw new ProtocolException("malformed JSON envelope", e);
        }
    }

    @Override
    public void handleTransportError(WebSocketSession session, Throwable exception) {
        LOG.warn("Transport error on {}: {}", session.getId(), exception.getMessage());
    }

    @Override
    public void afterConnectionClosed(WebSocketSession session, CloseStatus status) {
        registry.remove(session.getId());
        LOG.info("Client disconnected: {} ({}) — race sessions are kept for reconnection", session.getId(), status);
    }
}
