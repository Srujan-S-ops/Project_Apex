# PROJECT APEX 2.0 — Phase 1: Requirements & Planning

## 1. Product vision
A premium, playable 3D GT3 racing game for Windows. One original ~3.5 km street circuit
(Monaco/Baku inspired), a physically simulated GT3 car, four physics-driven AI opponents with
distinct personalities, two game modes, a motorsport-grade UI and a Java intelligence/persistence
backend.

## 2. Environment audit (performed 2026-11-20)

| Item | Result | Decision |
|---|---|---|
| Unity | 6000.3.28f1 (Unity 6.3 LTS), Windows Standalone support installed | Used. Project cloned from Unity's bundled URP template |
| Render pipeline | URP 17.x (template) | Used, Forward+ renderer |
| Java | Temurin 21.0.13 | Used |
| Maven / Gradle | Not installed | Apache Maven 3.9.11 downloaded from Maven Central into `.tools/` (git-ignored) |
| Python | 3.14 + Pillow + NumPy | Used for offline texture / branding generation |
| Blender / DCC tools | Not installed | Vehicle and environment meshes built procedurally in C# |
| Higgsfield | Connected, **0 credits, free plan**; connector states it *cannot generate media* | Not usable for production. See §6 |
| GPU | RTX 4050 Laptop | Target: 60 fps @1080p High |

## 3. Functional requirements

### FR-1 Vehicle
- Rigidbody + 4 WheelColliders, RWD, suspension, speed-sensitive steering with Ackermann geometry.
- Engine torque curve, 6-speed automatic gearbox with reverse, rev limiter.
- Brakes with bias, ABS, traction control (both toggleable), aerodynamic downforce and drag.
- Tyre friction model via tuned WheelCollider friction curves, anti-roll bars.
- Collision response, recovery (manual reset + automatic stuck/flip recovery).
- Visual model replaceable without touching physics (`CarVisualBuilder` is decoupled from `CarController`).

### FR-2 Circuit
- Closed-loop spline circuit ≈3.5 km with straights, hairpin, chicane, technical and fast sections, elevation.
- Pit lane, start grid, start/finish line, 3 sectors, checkpoints, barriers, kerbs, run-off.
- Environment: grandstands, pit building + garages, paddock, floodlights, advertising, catch fencing,
  road markings, braking boards, spectator areas, city buildings, harbour.

### FR-3 AI
- 4 opponents; personalities Aggressive / Defensive / Balanced (+ one extra Balanced/Aggressive variant).
- Difficulty Easy / Medium / Hard.
- Physics-driven (AI produces throttle/brake/steer inputs into the same `CarController` as the player).
- Waypoint + racing-line navigation, corner-speed profile, braking zones, avoidance, overtaking,
  defending, recovery.
- High-level intelligence in Java (track analysis + tactical decisions); low-level control in Unity.
- Offline fallback so the game remains playable if the backend is unavailable.

### FR-4 Backend (Java 21 / Spring Boot)
- WebSocket endpoint, JSON (Jackson), H2 persistence, JUnit tests.
- Track analysis, personalities, AI decisions, race sessions, lap/sector calculation, leaderboard,
  results, persistence of race history / player stats / lap records / classifications.
- Robustness: disconnects, timeouts, invalid messages, reconnection.

### FR-5 Game modes
- Quick Race (vs 1–4 AI, difficulty, 1–10 laps, grid, 5-light start, live leaderboard, timing, results).
- Time Trial (solo, current/previous/best lap, sectors, live delta, personal records, restart, stats).

### FR-6 UI/UX
Main Menu, Garage, Race Setup, HUD, Pause, Results, Settings, Loading. Black / charcoal / racing red /
white / metallic palette, animated controls, no default Unity styling. All data bound to live systems.

### FR-7 Audio
RPM-driven engine, gear shifts, exhaust pops, tyre screech, braking, collisions, crowd & pit ambience.

### FR-8 Presentation
URP lighting with shadows, HDR, post-processing, procedural sky, reflections, floodlights,
car lights, brake glow, tyre smoke, dust, sparks.

## 4. Non-functional requirements
- 60 fps target on the audit machine; physics at 100 Hz.
- No hard dependency on the backend at runtime (graceful degradation).
- Modular code (assembly-level namespaces), no duplicate classes, documented.

## 5. Acceptance criteria
A player launches `ProjectApex.exe`, navigates the menu, configures a race, drives against AI,
finishes and sees results; results are persisted by the backend and visible in the Garage/profile.

## 6. Asset production plan (revised after audit)
Higgsfield could not be used (zero credits and the connector exposes catalogue/browse tools only).
Rather than block, all assets are original and generated in-house:
- **Textures, branding, advertising, livery decals, loading art** → Python/Pillow generators in
  `Assets_Source/Tools`, output committed to `Assets_Source/` and imported into Unity.
- **3D vehicle and environment** → procedural mesh generation in C# (lofted body sections, real
  wheel/brake geometry). Visual layer is swappable for a DCC/AI model later.
- **Audio** → procedural synthesis at runtime (additive engine harmonics, filtered noise).
- **Cinematics** → real-time in-engine camera sequences instead of pre-rendered video.

## 7. Phase plan (Waterfall)
1 Requirements → 2 Architecture → 3 Core (vehicle, track, race) → 4 AI & race management →
5 UI/UX → 6 Java backend & DB → 7 Assets/environment/audio → 8 Integration → 9 Testing → 10 Build & docs.
