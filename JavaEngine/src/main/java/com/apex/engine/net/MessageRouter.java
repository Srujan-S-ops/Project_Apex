package com.apex.engine.net;

import com.apex.engine.ApexEngineApplication;
import com.apex.engine.ai.AICommand;
import com.apex.engine.ai.AIDecisionEngine;
import com.apex.engine.error.ApexException;
import com.apex.engine.error.ProtocolException;
import com.apex.engine.net.dto.Dtos;
import com.apex.engine.persistence.StatsService;
import com.apex.engine.race.LapRecord;
import com.apex.engine.race.RaceSession;
import com.apex.engine.race.RaceSessionManager;
import com.apex.engine.track.TrackAnalysis;
import com.apex.engine.track.TrackAnalyzer;
import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Qualifier;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

import java.util.List;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.ExecutorService;

/** Dispatches decoded envelopes to the engine services and replies to the client. */
@Component
public class MessageRouter {
    private static final Logger LOG = LoggerFactory.getLogger(MessageRouter.class);

    private final ObjectMapper mapper;
    private final TrackAnalyzer analyzer;
    private final RaceSessionManager sessions;
    private final AIDecisionEngine ai;
    private final StatsService stats;
    private final ExecutorService analysisExecutor;
    private final long standingsIntervalMs;

    public MessageRouter(ObjectMapper mapper, TrackAnalyzer analyzer, RaceSessionManager sessions, AIDecisionEngine ai,
                         StatsService stats, @Qualifier("analysisExecutor") ExecutorService analysisExecutor,
                         @Value("${apex.standings.interval-ms:500}") long standingsIntervalMs) {
        this.mapper = mapper;
        this.analyzer = analyzer;
        this.sessions = sessions;
        this.ai = ai;
        this.stats = stats;
        this.analysisExecutor = analysisExecutor;
        this.standingsIntervalMs = standingsIntervalMs;
    }

    public void route(ClientConnection c, Envelope env) {
        MessageType type = MessageType.fromWire(env.type())
                .orElseThrow(() -> new ProtocolException("unknown message type '" + env.type() + "'"));
        switch (type) {
            case HELLO -> {
                Dtos.Hello h = parse(env, Dtos.Hello.class);
                LOG.info("Client {} hello (v{})", c.id(), h.clientVersion());
                c.send(MessageType.WELCOME, new Dtos.Welcome(ApexEngineApplication.VERSION, c.id()));
            }
            case PING -> c.send(MessageType.PONG, parse(env, Dtos.Ping.class));
            case TRACK_DATA -> onTrackData(c, parse(env, Dtos.TrackData.class));
            case SESSION_START -> {
                RaceSession s = sessions.start(parse(env, Dtos.SessionStart.class));
                c.bindRaceSession(s.id());
            }
            case TELEMETRY -> onTelemetry(c, parse(env, Dtos.Telemetry.class));
            case LAP_COMPLETED -> onLap(parse(env, Dtos.LapCompleted.class));
            case RACE_FINISHED -> onRaceFinished(c, parse(env, Dtos.RaceFinished.class));
            case GET_PROFILE -> c.send(MessageType.PROFILE, stats.profile(parse(env, Dtos.GetProfile.class).trackId()));
            default -> throw new ProtocolException("message type '" + env.type() + "' is server-to-client only");
        }
    }

    private <T> T parse(Envelope env, Class<T> type) {
        if (env.payload() == null || env.payload().isBlank()) {
            throw new ProtocolException(env.type() + ": empty payload");
        }
        try {
            T value = mapper.readValue(env.payload(), type);
            if (value == null) {
                throw new ProtocolException(env.type() + ": null payload");
            }
            return value;
        } catch (JsonProcessingException e) {
            throw new ProtocolException(env.type() + ": invalid payload (" + e.getOriginalMessage() + ")", e);
        }
    }

    private void onTrackData(ClientConnection c, Dtos.TrackData data) {
        // CPU-heavy: run off the WebSocket thread, reply asynchronously
        CompletableFuture.supplyAsync(() -> analyzer.analyse(data), analysisExecutor)
                .thenAccept(a -> c.send(MessageType.TRACK_ANALYSIS, analyzer.toDto(a)))
                .exceptionally(ex -> {
                    Throwable cause = ex.getCause() != null ? ex.getCause() : ex;
                    String code = cause instanceof ApexException ae ? ae.getCode() : "INTERNAL";
                    LOG.warn("Track analysis failed: {}", cause.getMessage());
                    c.send(MessageType.ERROR, new Dtos.ErrorDto(code, cause.getMessage()));
                    return null;
                });
    }

    private void onTelemetry(ClientConnection c, Dtos.Telemetry t) {
        RaceSession s = sessions.get(t.sessionId());
        s.applyTelemetry(t);
        TrackAnalysis analysis = analyzer.forTrack(s.trackId());
        List<AICommand> decisions = ai.decideAll(s, analysis);
        if (!decisions.isEmpty()) {
            c.send(MessageType.AI_COMMANDS, new Dtos.AICommands(s.id(), decisions.stream().map(AICommand::toDto).toList()));
        }
        if ("QuickRace".equalsIgnoreCase(s.mode()) && c.standingsDue(standingsIntervalMs)) {
            c.send(MessageType.STANDINGS, new Dtos.Standings(s.id(), s.standings()));
        }
    }

    private void onLap(Dtos.LapCompleted m) {
        RaceSession s = sessions.get(m.sessionId());
        LapRecord r = s.recordLap(m);
        stats.saveLap(s, r);
    }

    private void onRaceFinished(ClientConnection c, Dtos.RaceFinished f) {
        if (f.sessionId() == null || f.results() == null) {
            throw new ProtocolException("raceFinished requires sessionId and results");
        }
        RaceSession s = sessions.find(f.sessionId());
        if (s != null) {
            s.finish();
        }
        c.send(MessageType.RACE_SAVED, stats.saveRace(f));
    }
}
