# Tidewall — Game Design Document

## Pitch
You are the last keeper of a crumbling sea wall. Every night the tide brings things up out of the surf: crabs, drowned men, armored shellbacks, swelling puffers, screaming gulls. Shoot them, fix the wall, hire a crew, rig the beach, and choose how greedy to be with the tide.

It plays like the browser wall-defense classics *The Last Stand* and *Bunny Invasion* (mouse-aim from a fixed wall, survive the night, spend between nights). What's new is the **beach as a gradient**: enemies come out of the sea **Wet** and dry off as they cross the sand. Wet changes how every element works (see `ELEMENTS.md`), so where you kill something matters as much as whether you kill it.

## Pillars
See `CLAUDE.md`: every shot is a decision, loadouts are answers, combos are explicit, readable chaos, short loop & long run.

## Core loop
```
 ┌─> SHOP (Armory / Fortify / Crew / Beach) ──> TIDE CARD (pick 1 of 3) ──> NIGHT (45–90 s) ──> SUMMARY ─┐
 └──────────────────────────────────────────────────────────────────────────────────────────────────────┘
                                         wall reaches 0 HP → GAME OVER
```
- **Night:** enemies spawn in packs from the sea on the right. You aim and shoot from the wall on the left. The night ends when every enemy is dead.
- **Summary:** kills, weak-point hits, perfect reloads, pearls, and a scrap breakdown (bounties + night bonus + wall-integrity bonus + scavengers, all times the card multiplier).
- **Shop:** spend scrap. Nothing you can do in the shop is irreversible except buying things (and selling refunds 50%).
- **Tide card:** a risk/reward modifier for the coming night. Harder cards pay more scrap.
- **Run:** surviving night 20 is a win. You can then keep going in endless mode. Boss nights: 5, 10, 15, 20.

## The field (logical 1280×720)
| Zone | x range | Notes |
|---|---|---|
| Wall | 40–190 | You stand at the front edge. Crew stand behind you. |
| Dry beach | 190 – ~700 | Enemies here are usually dry. Traps go here (and in the wet sand). |
| Wet sand | ~700 – 1080 | Enemies are drying off. Wet runs out about 3.5 s after an enemy leaves the water. |
| Sea | 1080–1280 | Enemies come out of the water here and are always Wet. |

## Player verbs
| Input | Action |
|---|---|
| Mouse | Aim (the arc is limited to the right side) |
| Left mouse (hold) | Fire (every weapon fires while held) |
| R | Reload / **active reload** (press again inside the bright zone) |
| 1 / 2 / 3, mouse wheel, Q | Switch weapon / swap to last weapon |
| Space, E | Use gadget 1 / gadget 2 |
| Esc / P | Pause |
| M | Mute |

### Active reload
While reloading, a bar fills under the crosshair. Press **R** inside the bright zone to finish the reload at once and get **Perfect** (+25% damage until the magazine is empty). Press outside it and you **Jam**: +0.6 s. Doing nothing is a normal reload. It's optional skill, never required.

### Weak points
Every enemy with a weak point draws it in a distinct way (eyes, crown, head fin). Hits there deal ×2 (more with the Hollow Point mod) and show yellow numbers.

### Pearls
Now and then a golden pearl bubble drifts across the sky. Shoot it for bonus scrap. It's a small aiming reward that competes for your attention with the horde.

## Shop tabs
- **Armory:** buy, tier up (1–5), and equip weapons into slots (2, buy a 3rd). Buy mods and socket them (1 socket, buy a 2nd per weapon). Buy and equip gadgets (1 slot, buy a 2nd).
- **Fortify:** repair the wall, Reinforce (max HP), Plating (flat damage reduction per enemy hit), Barbs (thorns), and buy extra slots.
- **Crew:** hire up to your number of posts (2, can grow to 4) and level each crew member to 3.
- **Beach:** buy traps and place them on the sand (3 trap slots, can grow to 5). Free to move.

A **Loadout bar** at the bottom of the shop always shows what you'll take into the night, plus any **synergies** it finds (e.g. "Conductor: shock + a wet source").

## Win / lose
- Lose: the wall hits 0. The game-over screen shows the night you reached, your stats, and the enemy type that did the most wall damage.
- Win: survive night 20 (the Leviathan). Then choose to keep going in endless mode.
- Save: the run autosaves when you enter the shop. Your best night is kept.

## Tone & presentation
Night-time beach under a big moon, in silhouette-friendly colors. Enemies have bright rim colors so they read against dark sand. Status effects tint and decorate enemies (blue sheen for wet, flames, frost, sparks, black gloss for oil). Everything is drawn in code.
