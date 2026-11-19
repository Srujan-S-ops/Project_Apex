# PROJECT APEX 2.0 — Phase 2: Architecture & Technical Design

## 1. System overview

```
┌──────────────────────── Unity 6.3 (C#, URP) ────────────────────────┐        ┌──────── Java 21 / Spring Boot ────────┐
│ Boot ─► MainMenu scene (Menu, Garage, Setup, Settings)               │        │ ApexWebSocketHandler (/apex)          │
│      ─► Race scene                                                    │  WS    │   └► MessageRouter                    │
│          TrackBuilder ─► TrackSpline (centre line, racing line)      │◄──────►│       ├► TrackAnalyzer (line + speeds) │
│          RaceManager ─► RaceParticipant[] ─► CarController (physics) │  JSON  │       ├► RaceSessionManager           │
│                         ▲ PlayerCarInput / AIDriver (ICarInput)      │        │       │    └► RaceSession / Leaderboard │
│          BackendClient (ClientWebSocket, reconnect, main-thread q)   │        │       ├► AIDecisionEngine (Executor)   │
│          HUD / Pause / Results (uGUI built by UIFactory)             │        │       │    └► DriverPersonality (poly) │
│          LocalFallback (C# speed profile + tactics, JSON records)    │        │       └► StatsService ─► H2 (JDBC)     │
└───────────────────────────────────────────────────────────────────────┘        └───────────────────────────────────────┘
```

Unity owns everything physical and real-time (60–100 Hz): vehicle dynamics, low-level steering /
throttle / brake control, collisions, timing gates. Java owns analysis and decisions (1–10 Hz):
racing-line optimisation, corner speed profile, braking zones, tactical AI state per driver,
session/leaderboard bookkeeping and persistence. If Java is unreachable the game falls back to
local C# equivalents and continues (requirement NFR "no hard dependency").

## 2. Unity project layout (`UnityGame/Assets/Apex`)

| Folder / namespace | Responsibility |
|---|---|
| `Core` (`Apex.Core`) | `GameBootstrap`, `GameServices`, `RaceConfig`, `GameSettings`, `SceneLoader`, `MathUtil` |
| `Vehicle` (`Apex.Vehicle`) | `CarSpec`, `CarController`, `ICarInput`, `PlayerCarInput`, `CarRecovery`, `CarVisualBuilder`, `CarEffects`, `CarAudio` |
| `Track` (`Apex.Track`) | `TrackLayout`, `TrackSpline`, `RacingLine`, `SpeedProfile`, `TrackBuilder`, `EnvironmentBuilder`, `MeshBuilder` |
| `Race` (`Apex.Race`) | `RaceManager`, `RaceParticipant`, `LapTimer`, `RaceResults`, `RecordsStore` |
| `AI` (`Apex.AI`) | `AIDriver`, `AIPersonality`, `AITactics` (local fallback) |
| `Net` (`Apex.Net`) | `BackendClient`, `Protocol` DTOs, `BackendLauncher` |
| `Rendering` (`Apex.Rendering`) | `MaterialLibrary`, `ProceduralTextures`, `PostFx`, `CameraRig`, `LightingSetup` |
| `Audio` (`Apex.Audio`) | `AudioSynth` (procedural clips), `AmbienceController` |
| `UI` (`Apex.UI`) | `UIFactory`, `UITheme`, screens: `MainMenuUI`, `GarageUI`, `RaceSetupUI`, `SettingsUI`, `LoadingUI`, `RaceHUD`, `Minimap`, `PauseUI`, `ResultsUI` |
| `Editor` | `ApexBuildPipeline` (project setup, materials, scenes, Windows build) |
| `Tests` | EditMode NUnit tests |

Scenes contain a single bootstrap object; all content is generated deterministically at load from
code + `Resources` assets. This avoids fragile hand-edited scene YAML, keeps every reference in
code (no broken inspector links) and makes the visual car/track layers swappable.

### Key design decisions
- **`ICarInput`**: the player and AI produce the same `CarInputState` (throttle, brake, steer,
  handbrake). `CarController` never knows who is driving ⇒ AI is genuinely physics-driven.
- **Template materials**: `Resources/Materials/*` are created by the editor pipeline with fixed
  keyword sets; runtime clones them. This guarantees URP shader variants survive build stripping.
- **Physics**: fixed step 0.01 s, WheelCollider substeps configured, CoM lowered, anti-roll bars.
- **Track spline**: centripetal Catmull–Rom through hand-designed control points, resampled at 2 m.
  All timing (sectors, laps, positions) uses arc-length progress from local nearest-sample search.

## 3. Race flow (state machine in `RaceManager`)
`Loading → Grid → Countdown(5 lights, random 0.4–1.6 s hold) → Racing → Finished(per car) → Results`.
Time Trial: `Grid → Racing(rolling laps) ↔ Pause`, restart = scene reload.

Lap validity: a lap counts only when every checkpoint (every ~100 m) was passed in order.
Leaving the track by more than half-width + 2.5 m flags the lap invalid (excluded from best lap /
records). Positions = laps completed then distance travelled.

## 4. Java backend design (`com.apex.engine`)

| Package | Classes | OOP concepts shown |
|---|---|---|
| `net` | `ApexWebSocketHandler`, `MessageRouter`, `Envelope`, `MessageType`, `ClientConnection`, `ConnectionRegistry`, `HeartbeatMonitor` | ConcurrentHashMap, ScheduledExecutorService, exception handling |
| `track` | `TrackModel`, `TrackAnalyzer`, `RacingLineOptimizer`, `SpeedProfileCalculator`, `Corner`, `CornerType` | encapsulation, records, enums |
| `ai` | `DrivingBehaviour` (interface), `DriverPersonality` (abstract), `AggressivePersonality`, `DefensivePersonality`, `BalancedPersonality`, `PersonalityFactory`, `Difficulty`, `AIDecisionEngine`, `DecisionContext`, `AICommand`, `TacticalMode` | inheritance, polymorphism, abstraction, ExecutorService + Futures |
| `race` | `RaceSession`, `RaceSessionManager`, `DriverState`, `LapCalculator`, `Leaderboard`, `StandingEntry`, `LapRecord` | synchronization, collections, comparators |
| `persistence` | `JdbcRepository<T,ID>` (generic abstract), `RaceRepository`, `LapRecordRepository`, `PlayerStatsRepository`, `StatsService` | generics, inheritance |
| `util` | `RollingWindow<T extends Number>` | bounded generics |
| `error` | `ApexException` → `ProtocolException`, `SessionNotFoundException`, `TrackAnalysisException`, `PersistenceException` | custom exception hierarchy |

H2 file database `JavaEngine/data/apexdb` (tables: `race`, `race_result`, `lap_record`, `player_stats`).

## 5. Protocol (WebSocket `ws://127.0.0.1:8765/apex`, UTF-8 JSON)

Envelope (kept flat so Unity's `JsonUtility` can read it):
```json
{ "type": "telemetry", "seq": 42, "payload": "{...escaped JSON...}" }
```

| Direction | type | payload |
|---|---|---|
| U→J | `hello` | `{clientVersion}` |
| J→U | `welcome` | `{serverVersion, connectionId}` |
| U→J | `ping` / J→U `pong` | `{t}` heartbeat every 2 s; server drops a client after 15 s silence |
| U→J | `trackData` | `{trackId, x[], y[], z[], width[], mu, massKg, downforceCoef, maxAccel, maxBrake, topSpeed}` |
| J→U | `trackAnalysis` | `{trackId, lineOffset[], speed[], curvature[], corners[{start,apex,end,minSpeed,type}], brakingZones[]}` |
| U→J | `sessionStart` | `{sessionId, mode, difficulty, laps, trackId, trackLength, drivers[{id,name,personality,isPlayer,car}]}` |
| U→J | `telemetry` (10 Hz) | `{sessionId, raceTime, cars[{id, distance, lapDistance, lateral, speed, lap, position}]}` |
| J→U | `aiCommands` | `{sessionId, commands[{id, mode, speedScale, lateralTarget, aggression}]}` |
| J→U | `standings` (2 Hz) | `{sessionId, entries[{id, position, gapToLeader, interval, lap, bestLap}]}` |
| U→J | `lapCompleted` | `{sessionId, driverId, lap, lapTime, s1, s2, s3, valid}` |
| U→J | `raceFinished` | `{sessionId, mode, trackId, laps, difficulty, results[{driverId, name, isPlayer, position, totalTime, bestLap, laps, dnf}]}` |
| J→U | `raceSaved` | `{raceId, newPersonalBest, personalBest}` |
| U→J | `getProfile` → J→U `profile` | `{races, wins, podiums, bestLap, totalLaps, recent[...], records[...]}` |
| J→U | `error` | `{code, message}` (invalid JSON, unknown type, unknown session …) |

Robustness: client reconnects with exponential backoff (0.5 → 8 s), queues `lapCompleted` /
`raceFinished` until reconnected, re-sends `trackData` + `sessionStart` after reconnect. AI uses
the last command for ≤1.5 s, then the local tactical fallback.

## 6. Build & deployment
`Build/Windows/ProjectApex.exe` + `Build/Windows/Backend/apex-engine.jar`. On start the game tries to
connect; if no server responds it launches the bundled jar (`java -jar`, hidden window) and retries.
The process is terminated when the game quits.
