# PROJECT APEX 2.0 — Known Limitations

## Environment / tooling
1. **Higgsfield was not usable.** The connected workspace has 0 credits on the free plan, and the
   connector only exposes catalogue and browse tools (no generation). All branding, textures,
   advertising, decals and key art were produced by the original procedural generator
   (`Assets_Source/Tools/generate_assets.py`). The 3D car and environment are procedural meshes
   built in code. Video cinematics were replaced by real-time in-engine camera work: the menu
   showroom orbit, the grid fly-in and TV cameras.
2. **Windows Application Control on the development machine blocks the freshly built, unsigned
   `ProjectApex.exe`** ("An Application Control policy has blocked this file"). This is an OS
   security policy, so it was left unchanged. Automated playtests therefore ran the same scenes
   and code in Unity Play Mode (editor), with the Java engine started from `JavaEngine/target`.
   To run the executable, sign it, or allow it in Windows Security (Smart App Control / App
   Control for Business). On machines without that policy it starts normally.
3. Maven was not installed. Apache Maven 3.9.11 was fetched into `.tools/`, which is git-ignored.

## Gameplay scope
- One circuit (Porto Vela, 3.5 km) and three GT3 cars (shared physics model, different specs and
  engine voices).
- No pit stops, tyre wear, fuel or damage model. The pit lane and garages are scenery behind the
  pit wall.
- The automatic gearbox is the only transmission mode; there are no manual paddles.
- The AI drives real physics. In lap-1 traffic it still makes light car-to-car contact and
  sometimes runs past the exit kerb at T1 (Bastione), which invalidates that lap (measured: 8 of 15
  laps valid in a 3-lap Hard race; leader always clean). No AI car got stuck or needed a respawn in
  any test run; automatic recovery exists for stuck > 6.5 s or flipped > 2.5 s.
- AI tyre grip was calibrated empirically (μ = 1.35 in the speed profile). Lap times are around 1:30
  on Hard.
- Keyboard and gamepad are supported. There is no force-feedback wheel support.

## Technical
- Text uses the Windows system font Bahnschrift via `Font.CreateDynamicFontFromOSFont`. On systems
  without it, Segoe UI or Arial is used.
- Legacy uGUI `Text` is used instead of TextMeshPro, to avoid importing TMP essential resources in
  batch builds.
- Audio is synthesised (original). It is convincing but not sampled from real cars.
- Reflections come from one realtime reflection probe rendered at load; there are no
  screen-space reflections.
