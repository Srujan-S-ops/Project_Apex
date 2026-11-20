# PROJECT APEX 2.0 — Real Racing

A complete 3D GT3 racing game for Windows: a physically simulated GT3 car, four physics-driven AI
rivals with distinct personalities, an original 3.5 km harbour street circuit, Quick Race and Time
Trial modes, a motorsport-style UI, and a Java 21 / Spring Boot engine for AI tactics, race
management and H2 persistence.

Two car classes: **GT3** (three ~550 hp sports cars) and **Formula** (three original open-wheel
single-seaters: ~1000 hp, 798 kg, 8-speed, high downforce, halo). Pick the class in the Garage or
in Race Setup; the AI field races in the same class.

## Play

```
Build\Windows\ProjectApex.exe
```

> **Note for this PC:** Windows Application Control currently blocks the unsigned, freshly built
> `ProjectApex.exe`. Allow it in Windows Security, or play from the editor: open `UnityGame` in
> Unity 6000.3.28f1, open `Assets/Scenes/MainMenu.unity` and press Play.

The game starts its Java engine on its own (`Build\Windows\Backend`, which bundles a trimmed
Java 21 runtime). If the engine can't start, the game switches to offline mode: AI uses the local
C# fallback and records are kept locally.

| Action | Keyboard | Gamepad |
|---|---|---|
| Throttle | W / ↑ | RT |
| Brake / reverse | S / ↓ | LT |
| Steer | A D / ← → | Left stick |
| Handbrake | Space | A |
| Change camera | C | Y |
| Look back | B | R3 |
| Reset car | R | View |
| Pause | Esc / P | Menu |

Menus: arrow keys / D-pad to move, Enter / A to confirm, ← → to change options, Esc / B to go back,
Q / E to switch settings tabs.

## Repository layout

| Path | Contents |
|---|---|
| `UnityGame/` | Unity 6.3 LTS (6000.3.28f1) URP project. All game code is in `Assets/Apex` |
| `JavaEngine/` | Spring Boot 3.5 / Java 21 engine (`mvn package` → `target/apex-engine.jar`) |
| `Assets_Source/` | Original generated textures and branding, plus the generator `Tools/generate_assets.py` |
| `Documentation/` | Requirements, architecture, testing report, known limitations |
| `Design/` | Track layout and UI design notes |
| `Build/` | Build and test scripts; `Build/Windows` holds the packaged game |

## Build from source

Requirements: Unity 6000.3.28f1 with Windows Build Support, JDK 21, Python 3 with Pillow and NumPy.
Maven is fetched into `.tools/` (or use your own).

```
powershell -ExecutionPolicy Bypass -File Build\build_all.ps1
```

The script generates the assets, builds and tests the engine, builds the Unity player in batch
mode, and bundles the engine with a jlink runtime. To work in the editor, open `UnityGame` in Unity
and run **Apex ▸ Setup Project** once; the scenes are `Assets/Scenes/MainMenu.unity` and `Race.unity`.

## Tests

```
cd JavaEngine && mvn test                                  # JUnit: unit + WebSocket/H2 integration
Unity -batchmode -projectPath UnityGame -runTests -testPlatform EditMode
powershell -File Build\run_autotest.ps1 -Laps 2 -Name race # automated full-race playtest
```

See `Documentation/04_Testing_Report.md` for results.

## Documentation

- [Requirements & planning](Documentation/01_Requirements.md)
- [Architecture & protocol](Documentation/02_Architecture.md)
- [Implementation notes](Documentation/03_Implementation.md)
- [Testing report](Documentation/04_Testing_Report.md)
- [Known limitations](Documentation/05_Known_Limitations.md)
- [Visual overhaul](Documentation/06_Visual_Overhaul.md) · [before/after](Documentation/screenshots/before_after.jpg)
- [Formula car class](Documentation/07_Formula_Class.md)
