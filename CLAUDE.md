# TideWall 3D — rules for Claude

This repo is **TideWall 3D**, a Unity 6 (6000.5.10f1, URP) co-op wall-defense **roguelike**: 1–4 players, in split screen and/or online, defend the Nuclear Sand Powerplant behind a breachable sea wall through days of phases (Morning → Evening → Night) until it melts down. It's first person by default, with a top-down view one button away. It grew out of **Tidewall 2D**, which now lives in the website repo (`Website/tidewall/`, with its own rules). The 3D game keeps the 2D game's core idea, the **wet/dry beach**, and builds on it.

The design documents in `docs/` define the game. These rules are binding for any change you make here. If a request conflicts with a rule, say which rule and ask before breaking it; if the user confirms, update the docs in the same change.

## Pillars (in priority order)
1. **Co-op first, couch or online.** Everything works for 1, 2, 3 or 4 players in split screen, online, or both mixed, with any controller or keyboard + mouse.
2. **Where you hit matters.** Shots go to the 3D point under your crosshair (first person) or reticle (top-down); limbs come off where you hit them, and that changes the enemy and what its corpse is worth.
3. **The tide is the game.** Enemies come out of the sea wet and dry off on the sand. Wetness is always visible (drips) and always matters (shock, fire).
4. **A day has a rhythm.** Mornings are calm and about economy (sell, buy, eat, spend points). Evenings and nights are fights, each opened by a tidal card vote. Phases are data.
5. **Every run is different, every loss is final.** Tidal cards, weapon mods and stat builds make runs diverge; a meltdown ends the run (no reloading a loss).
6. **Silly but readable.** Hotdog condiments, ??? sauce, exploding gas cans: chaos is welcome, but you always know who you are, where you're aiming, and how the wall is doing.

## Hard rules

### Data
- **R1 — Numbers live in data, not code.** Enemies, weapons, melee, food, condiments, upgrades and balance coefficients are in `GameDatabase` (`Assets/Scripts/Core/GameDatabase.cs` defaults → `Assets/Content/Data/GameDatabase.asset`). Phases are in `PhaseCycle` (`PhaseCycle.cs` defaults → `Assets/Content/Data/PhaseCycle.asset`). When you change a default in code, also update the asset (menu **TideWall → Reset Data To Defaults**, or edit it) so they agree.
- **R2 — Phases are pure data.** Code must never branch on a phase id or name (`"morning"`, `"night"`…). Behaviour comes from `PhaseDefinition` fields (`shopOpen`, `waves`, `endWhenAllReady`, `saveOnStart`, `lanterns`…). Adding a phase means adding a list entry, not code. If a new phase needs new behaviour, add a field to `PhaseDefinition`, document it in `docs/GDD.md`, and default it to "off".
- **R3 — New content fills the checklist** in `docs/GDD.md` → *Adding content* (model, behaviour, limbs, its own animation, question it asks, counter, corpse value, wet behaviour, what it does behind a breach).
- **R3a — Tidal cards, stats and mods are data.** Cards (`TidalCardDef`), stats (`StatDef`) and mods (`ModDef`) live in `GameDatabase`. A card that only changes numbers needs no code. A card lasts a number of **waves**, never seconds or phases.
- **R3b — Exclusive cards must be unique to their mode.** An Online card must need players on different computers, and a Split Screen card people sharing a screen. Its effect must be something no other card does. Availability goes through `TidalCards.Allowed` (`OnlineEligible` / `SplitEligible`); never offer an exclusive card outside its mode.

### Models (Blender)
- **R4 — `Blender/generate_models.py` is the source of truth for models.** Never hand-edit FBX files. Change the script and re-run it (see `docs/TECH.md`). Keep models low-poly: primitives, flat colours, no textures. Front faces **-Y** in Blender (= +Z in Unity). Each model's root is named after it.
- **R5 — Limbs follow the naming contract.** A severable part is a separate object named `Limb_<Part>` with its pivot at the joint, and a matching `LimbDef` in the enemy's data. `Limb_Head`/`Limb_Eyes` are weak points (`LimbEffect.Kill`, crit > 1). Attachment points are empties named `Socket_<Name>` (the keep's `Socket_Camera` is the main menu's viewpoint). A material named `coat` is recoloured per player.
- **R6 — The website models are imported, not redrawn.** Food, gas can, tire and Glizzy Monster come from `Blender/source/*.glb` (copied from the website) and are size-normalised by the script; heavy ones are welded and decimated to about 8k faces.

### Multiplayer & input
- **R6a — Menus never stop you walking.** Menus are driven by `MenuNav` (d-pad / arrow keys / wheel), never by `Move`. New menus use the `Menu` data and one of its layouts; stall menus set an `Anchor`. Item cards that show a thing set `MenuItem.Icon` to its model (the picture comes from `Icons`).
- **R6b — Reloads are optional minigames.** Every gun names a `reloadStyle`; doing nothing must still finish the reload in the gun's time, skill only speeds it up or earns a Perfect. Reload feedback goes at the reticle (`ReticleHud`), not in a screen corner.
- **R7 — Every action has a gamepad and a keyboard/mouse binding**, defined in `PlayerControls.Build` with a binding group (`Keyboard` or `Gamepad`). The first binding of each group is the rebindable one; rebinds live in `Settings.bindings` and every `PlayerControls` re-applies them. Prompts show the player's own key with `Player.Glyph(Btn)`, never a hard-coded key name. Gameplay reads buttons through `Player.Held/Pressed(Btn)`, which works for local and online players alike. Never read `Gamepad.current`, `Keyboard.current` or `Mouse.current` in gameplay code (the main menu's own UI input is the one exception).
- **R8 — Per-player things stay in the player's viewport.** HUDs, menus, overlays (ink, tunnel vision), the view model and camera effects belong to that player's camera/canvas. Team-wide state (coins, wall, phase, upgrades) is shared and shown to everyone.
- **R9 — Test with virtual devices.** Play-mode tests join players with `InputSystem.AddDevice<Gamepad>()`. A feature that needs input is tested by queuing device state, not by calling private methods.
- **R9a — Both views work.** Anything aimed or shown must work in first person (crosshair, eye camera, view model) and top-down (reticle, high camera). Your own body is on layer `Player.BodyLayer + index` and is hidden from your own first-person camera; anything you carry goes on that layer too.

### Online
- **R16 — The host owns the game.** Online, only the host simulates: enemies, the wall, coins, upgrades, phases, shop menus, and every player's health and ammo. Clients mirror snapshots and move/look their own players locally. So:
  - Code that changes shared state must only run where the game is simulated (`Player.Simulated`, `!Net.IsClient`). Code in the existing simulation paths already does.
  - Anything new that clients must **see** needs a `NetId` (tagged on the host with `NetId.Tag`, mirrored in `NetClient.ApplyEntity`) or a field in `NetPlayerState` / the snapshot header.
  - New effects go through `Fx` and sounds through `Sfx`: both are relayed to clients automatically (drips aren't; clients make them from wetness). Per-player feedback goes through `Player.Toast/Shake/OnHitEnemy/Push`, which reach online players too.
  - Changing the wire format means bumping `NetProtocol.Version`.

### Saves & settings
- **R17 — Never touch the player's files from tests or tools.** Settings and saves go through `Storage` (its `Root` is `Application.persistentDataPath`); tests point `Storage.Root` at a temp folder. The capture tour never saves or changes settings.
- **R18 — Saves are data too.** A save (`SaveData`) is a suspended run at a `saveOnStart` phase: day, phase, sand dollars, wall sections, powerplant, upgrades, active tidal cards, and each player slot's weapons, level, XP, stat points and mods. A run's save is deleted when the run ends (roguelike), never restored after a loss. If you add progress that should survive quitting, add it to `SaveData`, `SaveSystem.Capture` and the restore path (`GameManager.StartRun`, `Player.RestoreLoadout`), and keep old saves loading (new fields need sensible defaults).

### Feel & performance
- **R9d — World text** is 3D TextMeshPro made with `Ui.Label`, plus a `Billboard` (never legacy `TextMesh` or `UI.Text`; all text is TextMeshPro). Pieces that tile (the wall) are modelled without their joining faces so they read as one object.
- **R9c — Animated, not static.** Players and vendors are jointed (`Part_*`) and every creature has its own animation; new characters need both a model with parts and a pose function. Melee hits land at the end of the wind-up (`MeleeDef.windup`), never instantly.
- **R9b — Creatures arrive on the waves.** Anything that comes from the sea rides a `Breaker` in (flyers fly in); nothing pops into existence on the beach except boss summons and test/capture spawns.
- **R10 — Wetness is always visible.** Drip count is proportional to wetness (`Fx.Drip`, `maxDripsPerSecond`). Anything that changes wetness must keep drips and gloss in sync.
- **R10a — The money is sand dollars.** Never call it coins in anything a player sees; show the sand dollar icon (`Ui.SandDollarIcon`) or "SD" next to amounts.
- **R11 — Feedback for every hit**: hit flash, gore/sparks, a sound, a hit marker for the shooter; severed limbs fly off with physics; big events shake the cameras of nearby players (scaled by the screen-shake setting).
- **R12 — Shared effects, pooled objects.** Particles go through the shared systems in `Fx` (no per-enemy `ParticleSystem`). Tracers and light flashes are pooled. Enemies alive at once are capped by `GameDatabase.maxAlive`.
- **R13 — The level is the TideWall scene.** It was generated by `WorldBuilder` (TideWall → Rebuild World In Scene) and is now edited in Unity like any scene; numbers still come from data (`World.Init`). Things that exist only during play (players, creatures, loot) are spawned from the data in code. Assets are reached through the `ContentLibrary` (the scene's GameManager references it): never add a `Resources` folder or `Resources.Load`.
- **R13a — One component per file.** Every MonoBehaviour and ScriptableObject lives in a file named after it (Unity can't save it in a scene or asset otherwise). Split big classes by topic with `partial` (`Player.Movement.cs`, `Enemy.Damage.cs`…), keeping fields and the frame loop in the main file.
- **R13b — Online: the host decides, the wire is untrusted.** Movement is simulated by the host from input commands (clients predict and reconcile); never accept a position, damage or result from a client. Everything read from the network is bounded and validated (sizes, counts, finite numbers, ownership, text without rich-text tags) and a bad message drops only that client. Cosmetic things go unreliable; anything that must arrive goes reliable. New messages go in `NetProtocol` (and bump `Version`).
- **R13c — The HUD doesn't rebuild text every frame.** Rebuild a line only when its values change (`HudLine` / `Ui.Set`).

### Process
- **R14 — Verify before calling it done.** After a gameplay change: compile (headless setup run), run the play-mode tests, and run the screenshot tour (`-tidewall-capture`) for anything visual. For online changes, also run the two-process check (`-tidewall-host-demo` + `-tidewall-join`). Commands are in `docs/TECH.md`. Report failures as they are.
- **R15 — Unity 6.5 API.** Use `FindAnyObjectByType` / `FindObjectsByType<T>()` (no sort mode), `Rigidbody.linearVelocity`, and never `Object.GetInstanceID()` (it's an error in 6.5). Use fully qualified `[UnityEditor.MenuItem]` in editor code (the game has its own `MenuItem` class). Inside the `Menus` class, `Settings` is a method: write `TideWall.Settings` for the settings type.

## Where things are
| Doc | What it decides |
|---|---|
| `README.md` | What the game is, how to open, play, build and test it |
| `docs/GDD.md` | Design: views, phases, enemies, limbs, wetness, weapons, food, condiments, ??? sauce, economy, controls, settings, saves, online, main menu, adding content |
| `docs/TEXT.md` | Where every piece of game text lives (for editing it) |
| `docs/TECH.md` | Architecture, player kinds, online protocol, saves and settings storage, file map, the Blender pipeline, data assets, tests, capture modes, builds |
