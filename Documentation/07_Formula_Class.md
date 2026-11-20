# 07 — Formula car class

A second car class next to GT3. Nothing about GT3 changed: its specs, handling, records and
visuals are the same as before.

## Cars (original designs)

| Car | Maker | Power | Mass | Gears | Top speed |
|---|---|---|---|---|---|
| APEX FX-26 | Apex Engineering | ~999 hp | 798 kg | 8 | ~341 km/h (gearing) |
| VANTA VF-1 | Vanta Automobili | ~1020 hp | 798 kg | 8 | — |
| KAISER K-26 | Kaiser Motorsport | ~975 hp | 798 kg | 8 | — |

The engine is a 1.6 L V6 turbo hybrid, revving 4 000–12 500 rpm. ClA is 4.8 (GT3: 3.1), aero
balance 42 % front, wheelbase 3.6 m. Chassis: stiff springs and dampers, 9 cm of suspension
travel, CoM 0.24 m and 405/305-style rear/front tyre widths.

## How it is wired

- **`CarSpec`**
  - `CarClass { GT3, Formula }`, `FormulaRoster` and `RosterFor(class)`.
  - Chassis values are per-spec fields (CoM height, suspension travel, springs, dampers, anti-roll,
    wheel mass, tyre widths, hood camera). Their defaults are the original GT3 constants.
- **`CarController`** reads those fields; for GT3 the result is identical.
- **`FormulaVisualBuilder`**
  - Procedural single-seater with the same `CarVisual` hierarchy as the GT3 builder.
  - Parts: monocoque and nose loft, sidepods with intakes, shark fin, floor, diffuser,
    multi-element front wing, rear and beam wing, halo, cockpit with helmet, mirrors, wishbones
    and push rods, rain light, sponsor and number decals.
  - Exposed wheels reuse `CarVisualBuilder.BuildWheel` with per-axle tyre width.
- **`CarFactory`** picks the builder and a narrow collider set per class. Formula colliders sit
  ≥ 0.26 m above the road.
- **`RaceConfig.carClass`**:
  - The player car and the AI field come from the class roster.
  - The AI speed profile uses `VehicleEnvelope.Formula` (mu 1.5, 800 kg, 10 / 17 m/s² accel /
    brake, 90 m/s cap). It is sent to the Java engine through the existing `TrackData` fields,
    with no protocol change.
  - Track dressing still uses the GT3 envelope, so the circuit is unchanged.
- **Records:** Formula laps are stored under `<mode>_F1`, so they never overwrite GT3 personal
  bests.
- **Audio:** each Formula car has its own high-revving V6 voice in `EngineVoice.ForCar`.
- **UI:**
  - Garage and Race Setup have a CLASS stepper.
  - Garage stat bars use one scale for both classes.
  - The gearbox label shows the real gear count.
- **Autotest:** `-apexClass Formula`.

## Collision events fix (affects both classes)

`ContinuousSpeculative` CCD reports contacts before bodies touch; the margin is ~0.8 m at 80 m/s.
`OnCollisionEnter` fired for road surface the car was only sliding over, which:

- spawned phantom sparks
- inflated the contact statistics (≈2000 "ground hits" per Formula race)

A contact now counts only if its gap closes within one physics step:
separation ≤ closing speed × fixedDeltaTime + 2 cm. Physics forces are unchanged.

## Results (editor Play Mode, HIGH preset, 2 laps, Hard AI, Java engine online)

| Run | Errors | Best laps | Top speed | Impacts | fps avg |
|---|---|---|---|---|---|
| Formula | 0 | 1:18.7 – 1:21 | 279–287 km/h | 10 | 138 |
| GT3 (regression) | 0 | 1:29.9 – 1:35.5 | 236–241 km/h | 5 | 129 |

In both runs all cars finished, with no recoveries. EditMode tests: 15/15, including the new
`FormulaRosterIsLightPowerfulEightSpeed`. Java: 26/26. Windows build: 188 MB, 0 errors.

Screenshots: `screenshots/formula/`.
