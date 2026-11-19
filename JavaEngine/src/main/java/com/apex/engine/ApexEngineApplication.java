package com.apex.engine;

import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.scheduling.annotation.EnableScheduling;

/**
 * PROJECT APEX 2.0 engine: track analysis, AI decision making, race session management and
 * persistence, served to the Unity client over a WebSocket at ws://127.0.0.1:8765/apex.
 */
@SpringBootApplication
@EnableScheduling
public class ApexEngineApplication {
    public static final String VERSION = "2.0.0";

    public static void main(String[] args) {
        SpringApplication.run(ApexEngineApplication.class, args);
    }
}
