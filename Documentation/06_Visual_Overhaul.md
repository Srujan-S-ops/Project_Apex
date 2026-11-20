# PROJECT APEX 2.0 — Visual Overhaul

Goal: move the presentation toward the look of a modern commercial street racer (reference:
*Need for Speed: Most Wanted*, 2012) using **original** assets and implementations. Nothing from that
game was copied.

Before/after comparison: `Documentation/screenshots/before_after.jpg`. Full sets are in
`screenshots/before/` (v1.0 build) and `screenshots/after/` (HIGH preset). Git tag
`v1.0-pre-visual-overhaul` preserves the original working version.

## Untouched systems
Vehicle physics, WheelColliders and the car hierarchy, the race system, AI, telemetry, the WebSocket
protocol and the Java backend were not modified. Road geometry and colliders are unchanged; only their
materials were replaced. The camera change only affects how obstruction checks treat other cars.

## Assets
| Source | Use | Licence |
|---|---|---|
| ambientCG Asphalt031, PavingStones138, Concrete034, Facade001/006/017/018A, Bricks105, Metal049A (2K PBR) | road, sidewalks, barriers, building façades, rims | CC0 1.0 (see `Assets_Source/Downloaded/LICENSES.md`) |
| `Assets_Source/Tools/process_pbr.py` (original) | converts the sets to URP layout (albedo, GL normal, metallic+smoothness, AO); generates road wear / tar snakes, paint-flake normal, kerb relief, line paint, car contact shadow, foliage, traffic signs | own work |
| `Apex/PlanarReflection` shader + `PlanarReflection.cs` (original) | mirrored-camera planar reflection for the showroom floor | own work |
| TextMeshPro essentials (bundled with Unity's ugui package) | UI text rendering | Unity package |

No new package dependencies were added.

## Graphics foundation
- **URP / Forward+:**
  - HDR and SMAA High on HIGH and above.
  - 4-cascade soft shadows (soft-shadow quality High).
  - Reflection-probe blending and box projection.
  - The existing SSAO renderer feature, toggled per preset.
- **Lighting:**
  - Late-afternoon sun (31° elevation, warm white, intensity 2.6) with sky-driven spherical-harmonics
    ambient light (`AmbientMode.Skybox` + `DynamicGI.UpdateEnvironment`).
  - Cooler linear haze from 220 m to 1,900 m.
- **Reflections:** a global probe plus 5–14 box-projected probes along the circuit (count and
  resolution depend on the preset), each rendered once at load.
- **Post-processing:**
  - ACES tonemapping and restrained bloom (threshold 1.15, intensity 0.32).
  - Grading: contrast +16, saturation −6, teal-shadow / warm-highlight split toning, slight warm white
    balance, vignette.
  - HIGH and above add fine film grain, light chromatic aberration and speed-driven camera motion blur.
    The blur is 0 below 100 km/h and is forced to 0 across camera cuts and teleports.

## Vehicle
- Paint uses URP **Complex Lit with clear coat**: metallic base, flake normal map and a clear-coat
  smoothness of 0.96. Carbon parts are clear-coated too.
- Body resolution raised from 32×70 to 48×110 cross-sections.
- **Glass:** deeper tint with black rubber window seals.
- **Headlights:** chrome reflector housings, twin projector lenses, a 7-segment LED running-light strip
  and clear lens covers.
- **Tail lights:** 22-segment LED bar plus clusters behind red glass, a rear fog light, and a high-level
  brake light. All are driven by the existing brake-light logic.
- Rear valance with number plate, front air curtains, roof scoop and antenna, chrome mirror glass.
- **Wheels:** brushed-metal PBR rims with machined chrome lips; brake glow is unchanged.
- A soft contact shadow grounds every car.

## Environment
- **Road:**
  - PBR asphalt in roughly 3.5 m tiles, with normal, roughness and AO maps.
  - A large-scale detail layer for repaired patches, tar snakes, oil drips and rubbered lanes.
  - Run-off in the same asphalt, darker and rougher.
  - Kerbs with a rumble-strip normal map.
  - Worn alpha-cut-out paint on the edge lines, plus new dashed lane separators on the straights.
- **Barriers:** concrete PBR.
- **Buildings** are modular:
  - Base: a glazed shopfront with a sponsor sign band and coloured awnings, or a brick base in the old
    town.
  - Body: real-scale PBR façades (glass curtain wall, office, columned tower, brick residential, brick).
  - Brick buildings get string courses.
  - Top: cornice with roof, then setback towers with masts and rooftop AC units or water tanks.
  - Every building gets a random texture offset to break up repetition.
- **Streetscape** behind the barriers: granite kerbstones and sidewalks, double-arm street lamps, trees
  in tree pits, benches with bins, glass bus shelters, and traffic signs (no parking, 50 km/h,
  pedestrian crossing, keep right). All are batched per material.

## Showroom
- Mirror-camera planar reflection with an oblique clip plane, 5-tap blur, fresnel and radial fade.
- The turntable was made flush so the reflection shows directly under the car.
- Commercial light rig:
  - Key, fill, cool and red rim lights, a top light and a ground bounce.
  - Twin overhead strip lights, which create long highlight lines along the clear coat.
  - An LED floor ribbon and a lighter cyclorama.
- Bokeh depth of field.

## UI / HUD
- All text moved from legacy `UI.Text` to **TextMeshPro**. Font assets are built at runtime from the
  installed Windows Bahnschrift, so the font file is never redistributed. A symbol-font fallback covers
  arrows and similar glyphs.
- Slanted (italic) numerals on the speed, gear, position, lap, timing and delta readouts. A larger
  speed readout and a dark backplate behind the tachometer.
- Layouts use a 1920×1080 reference with `CanvasScaler` (match 0.5), so they scale proportionally to
  2560×1440. Verified at 1080p; 1440p was not captured (the editor game view was fixed at 1080p).

## Graphics presets (measured, editor Play Mode, RTX 4050 Laptop, 1080p, 1-lap Hard race)
| Preset | What changes | Avg fps | 1 % low |
|---|---|---|---|
| LOW | 0.85 render scale, 1 cascade / 90 m shadows, half-res textures, no MSAA, no SSAO, no track probes, minimal post | 59.4* | 57.8 |
| MEDIUM | 2 cascades / 150 m, 2× MSAA, 5 probes (128 px), grading + vignette | 143 | 100 |
| HIGH | 4 cascades / 230 m, 4× MSAA, SSAO, 10 probes, full post incl. motion blur | 143 | 81–90 |
| ULTRA | 340 m shadows, 8× MSAA, 14 probes (256 px), HQ bloom / motion blur | 143 | 81 |

\* The LOW run is a flat ~60 fps line, consistent with the editor throttling an unfocused window rather
than GPU load. The other presets hit the editor's 144 fps cap.

## Verification after the overhaul
Every run below covered menu → settings → garage → setup → grid → 5-light start → race → pause/resume →
results, with 0 logged errors and the Java engine online (AI racing line supplied by Java). Lap timing,
sector events and H2 persistence all worked (the driver profile updated across runs).

| Run | Preset | Laps | Result |
|---|---|---|---|
| q0–q3 | LOW–ULTRA | 1 | all 5 cars finished |
| vis3 | HIGH | 2 | all 5 finished, 4 valid laps; pause froze the race clock |

## Honest limits
- The car and city are generated procedurally rather than hand-modelled. Materials, lighting and
  composition close much of the gap, but they don't reach a AAA studio's hand-built art.
- Sidewalk props have no colliders: they sit behind the barriers and can't be reached.
- The standalone `.exe` is still blocked on this PC by Windows Application Control (unsigned binary),
  so verification ran in editor Play Mode with identical scenes and code.
