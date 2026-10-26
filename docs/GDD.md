# Tidewall — Game Design Document

## Pitch
You are the last keeper of a crumbling sea wall. Every night the tide brings things up out of the surf: crabs, drowned men, armored shellbacks, swelling puffers, screaming gulls. Shoot them, fix the wall, hire a crew, rig the beach, and choose how greedy to be with the tide.

It plays like the browser wall-defense classics *The Last Stand* and *Bunny Invasion* (mouse-aim from a fixed wall, survive the night, spend between nights). What's new is the **beach as a gradient**: enemies come out of the sea **Wet** and dry off as they cross the sand. Wet changes how every element works (see `ELEMENTS.md`), so where you kill something matters as much as whether you kill it.

## Pillars
See `CLAUDE.md`: every shot is a decision, loadouts are answers, combos are explicit, readable chaos, short loop & long run.

## Core loop
```
 ┌─> SHOP (Armory / Fortify / Crew / Beach / Reliquary) ──> TIDE CARD (pick 1 of 3) ──> NIGHT (2–5 waves) ──> SUMMARY ─┐
 └────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘
                                         wall reaches 0 HP → GAME OVER
```
- **Night:** enemies spawn in packs from the sea on the right, in **waves** (2 on early nights, up to 5 later). You aim and shoot from the wall on the left. The night ends when every enemy is dead.
- **Waves rush:** each wave has a planned start, but **once half of the current wave is dead, the next wave arrives 3 seconds later**. Killing fast is rewarded with a shorter, more intense night, never with a lull. A banner announces each wave ("The next one rushes in!" when it was rushed).
- **Summary:** kills, weak-point hits, perfect reloads, pearls found, and a scrap breakdown (bounties + night bonus + wall-integrity bonus + scavengers, all times the card multiplier).
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
| R | Reload / play the gun's **reload minigame** (hold R for the Blowtorch) |
| 1 / 2 / 3, mouse wheel, Q | Switch weapon / swap to last weapon |
| Space, E | Use gadget 1 / gadget 2 |
| Esc / P | Pause |
| M | Mute |

### Reload minigames
Every gun reloads differently, and its reload is part of its personality. A widget appears under the crosshair while you reload, and the HUD's bottom line says what to do. All of them are optional: **if you do nothing, the gun reloads in its normal time.** Played well, a reload is faster and earns a **Perfect** (bonus damage until the magazine is empty). Played badly, it costs time.

| Gun | Style | How it plays | Miss | Perfect |
|---|---|---|---|---|
| Service Revolver | **Rhythm** | A metronome ticks. Tap R on each beat to snap in one round (beats come faster than the passive loading). | Off-beat tap: fumble, loading pauses 0.35 s | every round tapped in on the beat |
| Scattergun | **Shell by shell** | Shells load one at a time; tap R as each shell's marker crosses the green zone to slam it in. **Fire at any time** to stop with what's loaded. | Slip: that shell takes +0.25 s | every shell slammed in |
| Rivet SMG | **Active** | One tap in the green zone. | Jam +0.6 s | +25% |
| Longrifle | **Bolt action** | Two taps: open the bolt in the first zone (skips ahead), close it in the second. | Jam +0.5 s | both zones hit |
| Harpoon Gun | **Heavy** | A faster base reload (1.25 s). The marker **accelerates** into a narrow zone; one chance. | **Hard jam +1.4 s** | **+40%** |
| Mortar Tube | **Drop** | Like Active, but the zone moves to a new spot every reload. | Jam +0.6 s | +25% |
| Arc Caster | **Capacitor** | Mash R to charge faster. No penalty. | — | charged in under 55% of the normal time |
| Blowtorch | **Pressure** | Hold R to build pressure; release in the green. Releasing early just resets. | Hold too long: **vents**, +0.8 s, no more tries | release in the green |

Design intent: light weapons get forgiving, rhythmic reloads; heavy ones get fewer, harsher, higher-reward chances; energy and fuel weapons get physical verbs (mash, hold).

### Weak points
Every enemy with a weak point draws it in a distinct way (eyes, crown, head fin). Hits there deal ×2 (more with the Hollow Point mod) and show yellow numbers.

### Pearls and the Reliquary
Now and then a golden pearl bubble drifts across the sky; shoot it for a **pearl** (◉). Bosses drop 3. Pearls are a separate currency that **only** buys **relics** in the Reliquary: one-off, run-long upgrades that bend the rules (wider reload windows, wet lasting longer, see through fog…). Scrap can't buy them, so pearls reward aim, not grinding. The full list is in `LOADOUTS-AND-CONTENT.md`.

## Shop tabs
- **Armory:** buy, tier up (1–5), and equip weapons into slots (2, buy a 3rd). Buy mods and socket them (1 socket, buy a 2nd per weapon). Buy and equip gadgets (1 slot, buy a 2nd).
- **Fortify:** repair the wall, Reinforce (max HP), Plating (flat damage reduction per enemy hit), Barbs (thorns), and buy extra slots.
- **Crew:** hire up to your number of posts (2, can grow to 4) and level each crew member to 3.
- **Beach:** buy traps and place them on the sand (3 trap slots, can grow to 5). Free to move.
- **◉ Reliquary:** spend pearls on relics.

A **Loadout bar** at the bottom of the shop always shows what you'll take into the night, plus any **synergies** it finds (e.g. "Conductor: shock + a wet source").

**The shop scene.** The shop is daytime, seen from inside the castle through a wide window: people play beach volleyball between striped umbrellas, while a cut-away of the sea shows the night's creatures huddled around a battle plan. It's a calm breather between nights, with a hint of what's coming. The shop UI is laid out at 1560×878 and scaled to fit, so more fits on screen.

## Win / lose
- Lose: the wall hits 0. The game-over screen shows the night you reached, your stats, and the enemy type that did the most wall damage.
- Win: survive night 20 (the Leviathan). Then choose to keep going in endless mode.
- Save: the run autosaves when you enter the shop. Your best night is kept.

## Tone & presentation
Night-time beach under a big moon, in silhouette-friendly colors. Enemies have bright rim colors so they read against dark sand. Status effects tint and decorate enemies (blue sheen for wet, flames, frost, sparks, black gloss for oil). Everything is drawn in code.
