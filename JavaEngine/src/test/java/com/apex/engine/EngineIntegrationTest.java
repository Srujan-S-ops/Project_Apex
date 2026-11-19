package com.apex.engine;

import com.apex.engine.net.Envelope;
import com.apex.engine.net.dto.Dtos;
import com.apex.engine.persistence.StatsService;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.test.web.server.LocalServerPort;
import org.springframework.web.socket.TextMessage;
import org.springframework.web.socket.WebSocketSession;
import org.springframework.web.socket.client.standard.StandardWebSocketClient;
import org.springframework.web.socket.handler.TextWebSocketHandler;

import java.util.List;
import java.util.concurrent.BlockingQueue;
import java.util.concurrent.LinkedBlockingQueue;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicLong;

import static org.junit.jupiter.api.Assertions.*;

/** Full-stack tests: real WebSocket server, router, AI engine and an in-memory H2 database. */
@SpringBootTest(webEnvironment = SpringBootTest.WebEnvironment.RANDOM_PORT, properties = {
        "server.address=127.0.0.1",
        "spring.datasource.url=jdbc:h2:mem:apextest;DB_CLOSE_DELAY=-1",
        "logging.file.name="
})
class EngineIntegrationTest {
    @LocalServerPort
    int port;
    @Autowired
    ObjectMapper mapper;
    @Autowired
    StatsService stats;

    private WebSocketSession ws;
    private final BlockingQueue<JsonNode> inbox = new LinkedBlockingQueue<>();
    private final AtomicLong seq = new AtomicLong();

    @BeforeEach
    void connect() throws Exception {
        var client = new StandardWebSocketClient();
        ws = client.execute(new TextWebSocketHandler() {
            @Override
            protected void handleTextMessage(WebSocketSession session, TextMessage message) throws Exception {
                inbox.add(mapper.readTree(message.getPayload()));
            }
        }, "ws://127.0.0.1:" + port + "/apex").get(5, TimeUnit.SECONDS);
        ws.setTextMessageSizeLimit(4 * 1024 * 1024);
    }

    @AfterEach
    void close() throws Exception {
        if (ws != null && ws.isOpen()) ws.close();
    }

    private void send(String type, Object payload) throws Exception {
        String inner = mapper.writeValueAsString(payload);
        ws.sendMessage(new TextMessage(mapper.writeValueAsString(new Envelope(type, seq.incrementAndGet(), inner))));
    }

    private JsonNode await(String type) throws Exception {
        long end = System.currentTimeMillis() + 8000;
        while (System.currentTimeMillis() < end) {
            JsonNode n = inbox.poll(200, TimeUnit.MILLISECONDS);
            if (n != null && type.equals(n.get("type").asText())) {
                return mapper.readTree(n.get("payload").asText());
            }
        }
        fail("no '" + type + "' message received");
        return null;
    }

    @Test
    void helloIsAnsweredWithWelcomeAndPingWithPong() throws Exception {
        send("hello", new Dtos.Hello("test"));
        assertEquals("2.0.0", await("welcome").get("serverVersion").asText());
        send("ping", new Dtos.Ping(12.5));
        assertEquals(12.5, await("pong").get("t").asDouble(), 1e-9);
    }

    @Test
    void invalidMessagesProduceErrorsWithoutDroppingTheConnection() throws Exception {
        ws.sendMessage(new TextMessage("{not json"));
        assertEquals("PROTOCOL", await("error").get("code").asText());
        send("doesNotExist", new Dtos.Hello("x"));
        assertEquals("PROTOCOL", await("error").get("code").asText());
        send("telemetry", new Dtos.Telemetry("unknown-session", 1f, List.of()));
        assertEquals("SESSION_NOT_FOUND", await("error").get("code").asText());
        assertTrue(ws.isOpen());
        send("hello", new Dtos.Hello("still alive"));
        assertNotNull(await("welcome"));
    }

    @Test
    void trackDataReturnsAnalysisAndTelemetryReturnsAiCommandsAndStandings() throws Exception {
        Dtos.TrackData track = TestTracks.stadium();
        send("trackData", new Dtos.TrackData("stadium", track.x(), track.y(), track.z(), track.width(), 1.55f, 1300f, 1.9f, 7.5f, 13.5f, 78f));
        JsonNode analysis = await("trackAnalysis");
        assertEquals(track.x().length, analysis.get("speed").size());
        assertEquals(2, analysis.get("corners").size());

        send("sessionStart", RaceLogicTest.start("it-session"));
        send("telemetry", RaceLogicTest.telemetry("it-session", 10f, 400f, 395f, 380f, 200f, 390f));
        JsonNode cmds = await("aiCommands");
        assertEquals(4, cmds.get("commands").size());
        for (JsonNode c : cmds.get("commands")) {
            assertTrue(List.of("RACE", "PUSH", "OVERTAKE", "DEFEND", "CONSERVE", "RECOVER").contains(c.get("mode").asText()));
        }
        JsonNode standings = await("standings");
        assertEquals(5, standings.get("entries").size());
    }

    @Test
    void raceResultsArePersistedOnceAndReflectedInTheProfile() throws Exception {
        send("sessionStart", RaceLogicTest.start("persist-1"));
        send("lapCompleted", new Dtos.LapCompleted("persist-1", 4, 1, 101.5f, 33f, 34f, 34.5f, true));
        send("lapCompleted", new Dtos.LapCompleted("persist-1", 0, 1, 99.0f, 33f, 33f, 33f, true));
        var results = List.of(
                new Dtos.ResultDto(4, "PLAYER", true, 1, 300f, 101.5f, 3, false),
                new Dtos.ResultDto(0, "VARGA", false, 2, 301f, 99.0f, 3, false));
        var finished = new Dtos.RaceFinished("persist-1", "QuickRace", "stadium", "Hard", "APEX R-GT3", 3, results);
        send("raceFinished", finished);
        JsonNode saved = await("raceSaved");
        long raceId = saved.get("raceId").asLong();
        assertTrue(raceId > 0);
        // duplicate submission after a reconnect → same id, no duplicate rows
        send("raceFinished", finished);
        assertEquals(raceId, await("raceSaved").get("raceId").asLong());

        send("getProfile", new Dtos.GetProfile("stadium"));
        JsonNode profile = await("profile");
        assertTrue(profile.get("races").asInt() >= 1);
        assertTrue(profile.get("wins").asInt() >= 1);
        assertEquals(101.5, profile.get("bestLap").asDouble(), 1e-3);
        assertEquals(99.0, profile.get("records").get(0).get("lapTime").asDouble(), 1e-3, "fastest lap of any driver first");
    }

    @Test
    void reconnectingClientCanReattachToItsSession() throws Exception {
        send("sessionStart", RaceLogicTest.start("reattach"));
        ws.close();
        connect();
        send("sessionStart", RaceLogicTest.start("reattach"));
        send("telemetry", RaceLogicTest.telemetry("reattach", 5f, 300f, 280f, 260f, 240f, 220f));
        assertEquals(4, await("aiCommands").get("commands").size());
    }
}
