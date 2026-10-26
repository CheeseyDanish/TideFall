# TideWall 3D

A couch co-op wall-defense game for **1–4 players in split screen**. Hold the sea wall through a day of phases:
- **Morning:** sell corpses, upgrade, and eat hotdogs (condiments optional, ??? sauce at your own risk).
- **Evening and Night:** sharks, saltwater crocs, sea urchins, octopuses and more come up the beach.

Shoot their limbs off, swing an anchor, throw a gas can, and keep the wall standing.

Built with **Unity 6.5 (URP)**. Every model was generated in **Blender** from a script. It's the 3D follow-up to [Tidewall 2D](https://github.com/CheeseyDanish/Website) (the browser game on my website).

## Highlights
- **Split-screen drop-in co-op.** Press any button on a controller (Xbox, PlayStation, Switch Pro, generic gamepads, HID joysticks) or any key to join, up to 4 players.
- **Phases you can edit.** Morning, Evening and Night are just entries in a data asset. Add, remove or reorder them without code.
- **14 sea creatures, 7 of them new to the 3D version:** sharks that beach themselves, death-rolling saltwater crocs, spiny sea urchins, ink-squirting octopuses, stinging jellyfish, hermit crabs and Clawback lobsters. The Glizzy Monster boss arrives every 5th night.
- **Limb damage.** Shoot a crab's claws off and it barely hurts. Take a gull's wing and it crawls. Cut a croc's tail and it can't swipe. Intact corpses sell for more.
- **Wet beach.** Creatures come out of the sea dripping; more drops = wetter. Wet things take extra shock damage and don't burn.
- **Nautical melee:** beach spade, cutlass, oar, boat hook (shock) and anchor.
- **The hotdog stand** (on tires): ketchup = rage, mustard = speed, ??? sauce = anything. That includes an upside-down screen, first-person POV, giant mode, and a 0.01% jackpot.
- **Gas cans that *may* explode.**

## Play
1. Install **Unity 6000.5.10f1** (via Unity Hub).
2. Open this folder as a project. On first open, run **TideWall → Setup Project**; it only needs to run once.
3. Press Play, or build with **TideWall → Build Windows**.
4. Press any button or key to join.

| | Gamepad | Keyboard + mouse |
|---|---|---|
| Move / aim | left / right stick | WASD / mouse |
| Shoot (throw when carrying) | RT | left click |
| Melee | RB | right click / F |
| Interact | A | E |
| Dash | LB | Space |
| Reload / swap gun | X / Y | R / Q |
| Pause | Start | Esc |

## For developers
- **Design:** [docs/GDD.md](docs/GDD.md)
- **Architecture and commands:** [docs/TECH.md](docs/TECH.md), covering setup, tests, builds, the screenshot tour and the Blender pipeline
- **Rules for AI-assisted changes:** [CLAUDE.md](CLAUDE.md)

```bash
unity test . --mode PlayMode --output Logs/TestResults.xml
```

## Status
Local split-screen multiplayer is complete. **Online play isn't implemented yet**; `docs/TECH.md` describes how to add it with Netcode for GameObjects. There's no save system yet: a run lasts until the wall falls.
