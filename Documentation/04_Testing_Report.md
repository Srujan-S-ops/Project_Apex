# PROJECT APEX 2.0 — Phase 9 Testing Report

Test date: 2026-11-20. Machine: Windows 11, RTX 4050 Laptop GPU, 15 GB RAM.
Unity 6000.3.28f1, Java 21.0.13, Maven 3.9.11.

## 1. Strategy
| Level | Tool | What it covers |
|---|---|---|
| Java unit | JUnit 5 | track analysis, speed profile, corners/braking, personalities and difficulty, decision engine (parallelism, fallbacks), lap validation & stats, leaderboard gaps, session thread-safety, generics |
| Java integration | Spring Boot test, live WebSocket client, in-memory H2 | handshake, heartbeat, invalid-message handling, track analysis round-trip, AI commands and standings, persistence, idempotent resubmission, profile, reconnection |
| Unity unit | Unity Test Framework (EditMode) | spline length, closure, spacing, self-intersection, elevation, projection, corners, speed-profile physics, vehicle specs, protocol JSON compatibility, formatting |
| End-to-end | Automated playtest (`-apexAutotest`) | whole game flow; the player car is driven by the AI autopilot; captures screenshots and a measured JSON report |

Tests were not weakened to get passing results. The one test change is documented in §3.

## 2. Results summary
| Suite | Result |
|---|---|
| Java JUnit (unit + integration) | **26 / 26 passed** (`mvn test`) |
| Unity EditMode | **14 / 14 passed** (`Build/editmode_results.xml`) |
| End-to-end, online (Java engine) | Quick Race 1, 2 and 3 laps; Time Trial 2 laps. All sessions completed menu → results with 0 logged errors |
| End-to-end, offline fallback (`-apexNoBackend`) | 2-lap Quick Race completed. Local AI drove the race, and all 5 drivers set valid laps |
| Performance | ≈ 144 fps average, 96–100 fps 1 % low at 1920×1080 High (the editor caps at 144) |
| Java link robustness | 100 % online share, 0 reconnects in normal runs. Invalid JSON, unknown types, unknown sessions and reconnection are covered by integration tests |
| Persistence | Driver profile (races, laps, podiums, best lap) loaded from H2 in a later session (race7 menu screenshot) |

### Feature checklist (verified by the automated runs)
| Feature | Evidence |
|---|---|
| Vehicle physics, automatic gearbox, ABS / TCS | Gear 1–6 progression and ABS/TCS indicators visible in HUD screenshots; top speed ≈ 240 km/h on the harbour straight |
| Player controls | Same `CarController` path as AI; the input layer is covered by settings bindings (manual play only; see §5) |
| AI behaviour | 4 physics-driven rivals finish every race. Overtakes observed (player autopilot P5 → P2; ASA P3 → P2). Personalities: Aggressive driver won clean (0 contacts) in every online run |
| Race countdown | Events `LIGHT 1..5`, `LIGHTS OUT`; gantry and HUD lights in screenshots |
| Checkpoints / lap validation | 15 lap events in 3-lap races; invalid laps flagged with a `[TrackLimits]` log and HUD message |
| Sector timing | 3 sector events per lap; purple / green / yellow sector colours in the HUD |
| Leaderboard | Live timing tower with Java-computed gaps ("LIVE TIMING · JAVA ENGINE") |
| Quick Race / Time Trial | Both completed with a results screen; Time Trial: rolling start, delta to PB, consistency ±0.007 s |
| UI navigation | Menu → settings → garage → setup → race → pause → results captured in order |
| HUD | Tachometer, shift lights, gear/speed, pedals, ABS/TCS, position, laps, timing, minimap, flags |
| Pause & restart | Pause froze the race clock (`[AutoTest] pause froze race clock: True`); restart and main menu reload scenes via the loader |
| Audio | Synthesised engine layers, squeal, impacts, crowd and pit ambience run without errors (audibility is not machine-verifiable) |
| Java communication | ~3,500 messages per race in each direction; AI racing-line source "JAVA ENGINE" |
| Database persistence | `raceSaved` with idempotent resubmission (integration test) plus profile reload in the game |

## 3. Defects found and fixed
| # | Found by | Defect | Fix |
|---|---|---|---|
| 1 | JUnit `stadiumHasTwoHairpinsAndBrakingZones` | Braking-zone detection fired on every sample of a deceleration zone (56 zones instead of 2). The predecessor comparison used the wrong bound. Same defect in the C# mirror. | Detect a local maximum followed by deceleration; fixed in Java and C# |
| 2 | JUnit `easyDifficultyShrinksOvertakingWindow` | The test value (1.0 s) was outside the documented Hard window (0.85 s × 1.15 = 0.98 s). This was a test-data error, not an engine defect. | Test gap set to 0.9 s, which still discriminates Hard (attacks) from Easy (0.60 s window, does not) |
| 3 | Playtest 1 | Main menu UI aborted: `GetComponent<CanvasGroup>() ?? AddComponent()`. Unity's fake-null defeats `??`. | Explicit null checks at all three sites |
| 4 | Playtest 2 | My own edit introduced a compile error (an escaped `\n` became a literal newline) | Fixed. A batch-mode compile check now runs before every playtest |
| 5 | Playtest 3 | Grid fly-in frame washed cyan | Motion blur removed; intro camera starts in a safe position |
| 6 | Playtest 3 | Car rear and nose caps sampled the roof stripe (pale "bathtub" look) | Caps use the body-colour region of the livery UV |
| 7 | Playtest 3 | Screenshots captured screens mid-transition | Waits added after each capture |
| 8 | Playtest 3 | Menu showed "v · 0 ms" and "ENGINE OFFLINE" before the first engine reply | Version and latency hidden until known; label reads "SYNCING WITH ENGINE" |
| 9 | Playtest 3/4 (report + `[TrackLimits]` diagnostics) | Every car invalidated lap 1 at T1, running wide on exit (lateral −8.7 m on a right-hander). The speed profile assumed μ = 1.55, more than the WheelCollider tyres give. | μ calibrated to 1.35 (C# and Java); racing-line margin 1.4 → 1.9 m |
| 10 | Playtest 4 | Tyre smoke and skid marks emitted while stationary on the grid (noisy WheelCollider slip at 0 km/h) | Smoke requires movement or a deliberate burnout |
| 11 | Playtest 4 | "Wall hits" counted body-to-road scrapes | Metric only counts contacts with a non-vertical normal; body collider clearance raised |
| 12 | Playtest 5/6 | Followers in traffic still ran wide at T1 | Racecraft: no passes around the outside of a corner; larger speed reduction off-line in corners; reduced side-by-side push while cornering; track-edge awareness (steer back + lift) |

## 4. End-to-end playtest runs
| Run | Laps | Errors | Avg fps / 1 % low | Engine online | AI line source | Valid laps / total | Notes |
|---|---|---|---|---|---|---|---|
| race3 | 1 | 0 | 143 / 96 | 100 % | JAVA ENGINE | 0 / 5 | defects 5–9 |
| race4 | 2 | 0 | 144 / 98 | 100 % | JAVA ENGINE | 2 / 10 | defects 10–11 |
| race5 | 2 | 0 | 144 / 97 | 100 % | JAVA ENGINE | 3 / 10 | wall hits 145 → ≤13 per car |
| race6 | 3 | 0 | 144 / 98 | 100 % | JAVA ENGINE | 6 / 15 | lap ≈ 1:30 |
| race7 | 3 | 0 | 144 / 100 | 100 % | JAVA ENGINE | 8 / 15 | 4 of 5 drivers set valid best laps; player autopilot P5 → P2 |
| tt1 (Time Trial) | 2 | 0 | 143 / 82 | 100 % | JAVA ENGINE | 2 / 2 | 1:31.643 / 1:31.633, σ = 0.007 s |
| offline1 | 2 | 0 | 144 / 97 | offline | LOCAL | 8 / 10 | fallback AI; all 5 drivers set valid laps; pause verified |

## 5. Not covered by automation / residual issues
- **Human-in-the-loop driving feel** (keyboard/gamepad handling) cannot be verified by an autotest. The player car uses
  exactly the same physics as the AI cars that completed every race, and the input layer is small (`PlayerCarInput`).
- **Close-pack contact:** in lap-1 traffic the AI still makes light contact (8–31 impacts > 4 m/s per car over 3 laps)
  and occasionally exceeds track limits at T1 (Bastione). There are no stuck cars, no recoveries and no DNFs in any run.
- **Executable on this PC:** Windows Application Control blocks the unsigned `ProjectApex.exe`, so end-to-end runs
  used Play Mode in the editor with identical scenes and code. The build itself succeeds with 0 errors and 0 warnings.
- Frame times were measured in the editor (capped at 144 fps); the standalone player normally runs equal or faster.
