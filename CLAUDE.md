# TideWall 3D — rules for Claude

This repo is **TideWall 3D**, a Unity 6 (6000.5.10f1, URP) local co-op wall-defense game: 1–4 players in split screen hold a sea wall through a day of phases (Morning → Evening → Night). It grew out of **Tidewall 2D**, which now lives in the website repo (`Website/tidewall/`, with its own rules). The 3D game keeps the 2D game's core idea, the **wet/dry beach**, and builds on it.

The design documents in `docs/` define the game. These rules are binding for any change you make here. If a request conflicts with a rule, say which rule and ask before breaking it; if the user confirms, update the docs in the same change.

## Pillars (in priority order)
1. **Couch co-op first.** Everything works for 1, 2, 3 or 4 players in split screen, with any controller or keyboard + mouse.
2. **Where you hit matters.** Shots go to the 3D point under your reticle; limbs come off where you hit them, and that changes the enemy and what its corpse is worth.
3. **The tide is the game.** Enemies come out of the sea wet and dry off on the sand. Wetness is always visible (drips) and always matters (shock, fire).
4. **A day has a rhythm.** Mornings are calm and about economy (sell, buy, eat). Evenings and nights are fights. Phases are data.
5. **Silly but readable.** Hotdog condiments, ??? sauce, exploding gas cans: chaos is welcome, but you always know who you are, where your reticle is, and how the wall is doing.

## Hard rules

### Data
- **R1 — Numbers live in data, not code.** Enemies, weapons, melee, food, condiments, upgrades and balance coefficients are in `GameDatabase` (`Assets/Scripts/Core/GameDatabase.cs` defaults → `Assets/Resources/Data/GameDatabase.asset`). Phases are in `PhaseCycle` (`PhaseCycle.cs` defaults → `Assets/Resources/Data/PhaseCycle.asset`). When you change a default in code, also update the asset (menu **TideWall → Reset Data To Defaults**, or edit it) so they agree.
- **R2 — Phases are pure data.** Code must never branch on a phase id or name (`"morning"`, `"night"`…). Behaviour comes from `PhaseDefinition` fields (`shopOpen`, `waves`, `endWhenAllReady`, `lanterns`…). Adding a phase means adding a list entry, not code. If a new phase needs new behaviour, add a field to `PhaseDefinition`, document it in `docs/GDD.md`, and default it to "off".
- **R3 — New content fills the checklist** in `docs/GDD.md` → *Adding content* (model, behaviour, limbs, question it asks, counter, corpse value, wet behaviour).

### Models (Blender)
- **R4 — `Blender/generate_models.py` is the source of truth for models.** Never hand-edit FBX files. Change the script and re-run it (see `docs/TECH.md`). Keep models low-poly: primitives, flat colours, no textures. Front faces **-Y** in Blender (= +Z in Unity). Each model's root is named after it.
- **R5 — Limbs follow the naming contract.** A severable part is a separate object named `Limb_<Part>` with its pivot at the joint, and a matching `LimbDef` in the enemy's data. `Limb_Head`/`Limb_Eyes` are weak points (`LimbEffect.Kill`, crit > 1). Attachment points are empties named `Socket_<Name>`. A material named `coat` is recoloured per player.
- **R6 — The website models are imported, not redrawn.** Food, gas can, tire and Glizzy Monster come from `Blender/source/*.glb` (copied from the website) and are size-normalised by the script; heavy ones are decimated to about 8k faces.

### Multiplayer & input
- **R7 — Every action has a gamepad and a keyboard/mouse binding**, defined in `PlayerControls` and shown in the lobby text and HUD. Generic joysticks get basic bindings too. Never read `Gamepad.current`, `Keyboard.current` or `Mouse.current` in gameplay code; read the player's own `PlayerControls`.
- **R8 — Per-player things stay in the player's viewport.** HUDs, menus, overlays (ink, tunnel vision) and camera effects belong to that player's camera/canvas. Team-wide state (coins, wall, phase, upgrades) is shared and shown to everyone.
- **R9 — Test with virtual devices.** Play-mode tests join players with `InputSystem.AddDevice<Gamepad>()`. A feature that needs input is tested by queuing device state, not by calling private methods.

### Feel & performance
- **R10 — Wetness is always visible.** Drip count is proportional to wetness (`Fx.Drip`, `maxDripsPerSecond`). Anything that changes wetness must keep drips and gloss in sync.
- **R11 — Feedback for every hit**: hit flash, gore/sparks, a sound, a hit marker for the shooter; severed limbs fly off with physics; big events shake the cameras of nearby players.
- **R12 — Shared effects, pooled objects.** Particles go through the shared systems in `Fx` (no per-enemy `ParticleSystem`). Tracers and light flashes are pooled. Enemies alive at once are capped by `GameDatabase.maxAlive`.
- **R13 — Runtime-built world.** `GameManager` boots itself into any scene and builds the level in code (`WorldBuilder`). Don't add gameplay objects to scenes by hand; the scene only holds post-processing.

### Process
- **R14 — Verify before calling it done.** After a gameplay change: compile (headless setup run), run the play-mode tests, and run the screenshot tour (`-tidewall-capture`) for anything visual. Commands are in `docs/TECH.md`. Report failures as they are.
- **R15 — Unity 6.5 API.** Use `FindAnyObjectByType` / `FindObjectsByType<T>()` (no sort mode), `Rigidbody.linearVelocity`, and never `Object.GetInstanceID()` (it's an error in 6.5). Use fully qualified `[UnityEditor.MenuItem]` in editor code (the game has its own `MenuItem` class).
- **R16 — Online play isn't implemented yet.** Multiplayer is local (split screen). If you add online play, read `docs/TECH.md` → *Online multiplayer (future)* first: the server must own enemies, the wall and coins.

## Where things are
| Doc | What it decides |
|---|---|
| `README.md` | What the game is, how to open, play, build and test it |
| `docs/GDD.md` | Design: phases, enemies, limbs, wetness, weapons, food, condiments, ??? sauce, economy, controls, adding content |
| `docs/TECH.md` | Architecture, file map, the Blender pipeline, data assets, tests, capture tour, builds |
