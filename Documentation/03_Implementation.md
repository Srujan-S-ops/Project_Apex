# PROJECT APEX 2.0 — Implementation Notes (Phases 3–8)

## Vehicle (Phase 3) — `Assets/Apex/Vehicle`
- **CarController**: Rigidbody (1280–1320 kg, lowered CoM) and four WheelColliders with tuned
  friction curves. RWD torque comes from a torque curve × gear × final drive × efficiency.
  - Six-speed automatic: shift-up at 95 % rpm under throttle; throttle/brake-dependent downshift
    thresholds; 90 ms torque cut on shifts. Reverse engages after 0.45 s of brake at standstill.
  - Rev limiter, clutch-slip launch model, engine braking.
  - Brakes: 7,800 Nm total with 62 % front bias. ABS (slip > 0.24 → pulsed release at ~14 Hz),
    TCS (rear slip > 0.28 → torque reduction, gradual recovery).
  - Speed-sensitive steering (30° → 8.5°) with true Ackermann angles per front wheel.
  - Aero: downforce `½ρ·ClA·v²`, split 43/57 front/rear at the axles; drag `½ρ·CdA·v²`;
    rolling resistance; anti-roll bars; optional counter-steer and yaw-damping assist.
  - 100 Hz physics with WheelCollider sub-stepping.
- **CarVisualBuilder** builds a procedural model, independent of the physics:
  - Body: 70 superellipse cross-sections lofted along the car, with wheel-arch cut-outs,
    tumblehome and a hood valley.
  - Glass greenhouse, roof and pillars. Carbon splitter with end plates, canards and side skirts.
  - Diffuser with fins. Swan-neck rear wing with a NACA-style profile, end plates and Gurney flap.
  - Mirrors, intakes, hood louvres, emissive head and tail lights, exhausts and tow hooks.
  - Number roundels and sponsor decals. Interior: seat, dash, roll cage, steering wheel and a
    helmeted driver.
  - Wheels: lathed tyres, 10-spoke rims, centre-lock nuts, discs that glow from brake heat, and
    non-rotating calipers.
  - Liveries are painted per car at runtime.
- **CarAudio**: four RPM layers × on/off-load, with equal-power crossfades and pitch tracking.
  Also tyre squeal from slip, wind, gear-shift thumps, backfires on lift-off and impact sounds.
- **CarEffects**: tyre smoke from slip, skid marks (trail renderers), dust off-track, collision
  sparks, exhaust flames with a light flash.

## Circuit (Phase 3) — `Assets/Apex/Track`
- `TrackLayout`: 40 hand-designed control points, clockwise. Scaled to 3,500 m; elevation runs from
  −8 m to +8 m.
- `TrackSpline`: centripetal Catmull-Rom, resampled every 2 m (1,750 samples), with signed
  curvature and windowed nearest-point projection.
- `TrackAnalysis` is the offline mirror of the Java analyser: racing line, speed profile, corners
  and braking zones.
- `TrackBuilder`:
  - Cambered road with mesh colliders.
  - Kerbs on corner insides and exits.
  - Run-off, with escape roads on the outside of slow corners.
  - Concrete barriers with a track-facing advertising strip, catch fence and poles.
  - Edge lines, chequered start line and grid boxes.
  - Start gantry with five real start lights (emissive meshes plus point lights).
  - IDW-interpolated terrain that follows the track elevation, with a harbour basin and water.
- `EnvironmentBuilder`:
  - Pit lane and two-storey pit building with lit garages and team boards. Roof logo sign, timing
    tower, and a paddock with team trucks and hospitality units.
  - Grandstands with crowd, roofs and fascia advertising.
  - Footbridges carrying banners. Floodlights: emissive heads, plus spot lights on High and Ultra.
  - 150/100/50 m braking boards.
  - City blocks (three façade styles with lit windows) and an outer skyline.
  - Harbour quay, palms and yachts. TV camera posts.
- Every mesh is batched per material.

## Race management & AI (Phase 4)
- **RaceManager**:
  - Countdown: five lights with a random 0.4–1.6 s hold before lights out.
  - Timing: 100 m checkpoints in order, three sectors, and line crossings interpolated within
    the physics step. Sector status: purple (session best), green (personal best), yellow.
  - Validity: track limits at half-width + 1.9 m invalidate the lap. Also detects wrong way,
    flipped cars and off-map cars, with automatic respawn and 3 s ghosting.
  - Gaps are time-based, using markers every 20 m. Flags: green, yellow, blue, white, chequered
    and track-limits.
  - Finish: leader finish → everyone finishes at their next line crossing → classification.
  - Time-trial delta against a stored reference lap.
  - Java protocol: track data, session start, telemetry at 10 Hz and lap events go out; AI
    commands and standings come back. Results are queued reliably until sent.
- **AIDriver** works in layers:
  1. Pure-pursuit steering on the racing line, using a speed-dependent look-ahead.
  2. Speed control from a braking-aware look-ahead over the speed profile, scaled by difficulty,
     personality and command.
  3. Tactical command from Java, falling back to `LocalTactics` if a command is older than 1.5 s.
  4. Reactive traffic layer: side choice for passes, follow gap, side-by-side spacing.
  5. Recovery: reverse when stuck, respawn after 6.5 s.

## UI (Phase 5) — `Assets/Apex/UI`
All UI is built in code with uGUI and procedurally drawn sprites (chamfered panels, skew bars,
rings, glows). Text uses Bahnschrift, the Windows system font, so no font file is redistributed.
- Screens:
  - Main Menu: live 3D showroom background with a cinematic camera.
  - Garage: orbit camera, spec bars and car selection.
  - Race Setup: live minimap preview of the track.
  - Settings: five tabs.
  - Loading screen, HUD, Pause, Results.
- HUD:
  - Driving: 270° tachometer arc with a redline zone, 10 shift LEDs, gear and speed, pedal bars,
    flashing ABS/TCS pills.
  - Race state: position, lap, race time, live timing tower (gaps come from Java when connected),
    flag banner, five-light countdown.
  - Timing: current, last, best and personal-best laps, coloured sectors, delta.
  - Minimap, messages, and an engine link indicator.

## Java engine (Phase 6) — `JavaEngine`
See the architecture document §4. Concepts are demonstrated as follows:
- **Inheritance / polymorphism / abstraction:** `DriverPersonality` is a template-method base
  class with `Aggressive`, `Defensive` and `Balanced` subclasses. `JdbcRepository<T,ID>` → 4
  repositories. The `ApexException` hierarchy.
- **Interfaces:** `DrivingBehaviour`.
- **Generics:** `JdbcRepository<T,ID>` and `RollingWindow<T extends Number>`.
- **Collections:** `TreeMap` time-at-distance maps, `Deque`, `List`, `Map`.
- **Multithreading:**
  - ExecutorService `invokeAll` with a deadline for per-driver decisions.
  - `CompletableFuture` track analysis on a dedicated pool.
  - `@Scheduled` heartbeat monitor.
- **Concurrency:** `synchronized` race sessions and `ConcurrentHashMap` registries and caches.
  `ConcurrentWebSocketSessionDecorator` makes sends thread-safe.
- **Exception handling:** custom exceptions are converted to `error` messages; the connection is
  never dropped because of an exception.

## Assets & audio (Phase 7)
- `Assets_Source/Tools/generate_assets.py` produces 40+ original textures:
  - Surfaces: asphalt, concrete and paving with normal maps; kerbs, carbon, mesh grille, brake
    disc, fence, water.
  - Advertising: eight fictional sponsor brands, advert strips, gantry banner, logo sign, timing
    tower, braking boards.
  - Scenery: crowd, three façades with emission maps, palm fronds, particle sprites.
  - Branding: logo, app icon and loading key art.
- `AudioLibrary` synthesises all sound at runtime:
  - Engines from per-cylinder combustion pulses with exhaust resonance and lope. Loops are made
    seamless by quantising the RPM layers to integer cycles per loop.
  - Tyre squeal, wind, backfires, impacts, crowd, pit lane (impact wrenches), city ambience,
    UI sounds and an original 100 BPM menu music loop.

## Integration (Phase 8)
- `ApexBuildPipeline` configures the project:
  - URP (HDR, soft shadows, 4 cascades, Forward+ renderer) and template materials.
  - Scenes, build settings and player settings; audio voices raised to 64 real voices.
  - Builds `Build/Windows/ProjectApex.exe`.
- `BackendLauncher` starts the bundled engine (`Backend/jre/bin/java.exe -jar apex-engine.jar`) if
  no engine answers, and stops it on quit. Command-line switches: `-apexNoBackend`,
  `-apexBackend ws://…`.
- Automated playtests use `-apexAutotest -apexScreens -apexOut <dir> -apexLaps N
  [-apexTimeTrial]`. They drive the menus, race with the player's car on autopilot, capture
  screenshots, and write `report_<mode>.json` and `exceptions.log`.
