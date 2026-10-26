# TideWall 3D — Game Design Document

## Pitch
Up to four friends, on one screen or online, defend a crumbling sea wall, in first person or from above. Every **morning** you sell the night's catch to the fishmonger, buy guns and upgrades, and grab a hotdog (choose your condiment carefully). Every **evening** and **night**, the sea sends crabs, sharks, saltwater crocs, octopuses and worse up the beach. Shoot their claws off, hook them with a boat hook, or swing an anchor. Then do it all again, one day harder.

It's the 3D, couch co-op follow-up to Tidewall 2D (the browser game on the website), keeping its central idea: **the beach is a gradient from wet to dry**, and wetness changes how fights go.

## Core loop
```
             ┌───────────────── one day ──────────────────┐
 Main menu ─► MORNING ──(everyone rings the bell / timer)─► EVENING ──(waves cleared)─► NIGHT ──(waves cleared, day +1)─┐
  new/load/   autosave, shop, fishmonger, hotdogs,            2 light waves                3 waves, boss every 5th day       │
  host/join   picnic food, gas cans on the beach                                                                          │
             ▲───────────────────────────────────────────────────────────────────────────────────────────────────────────┘
 Lose: the wall reaches 0 HP, or every player is down at once → try again from the last save, or back to the menu.
```

## Views
- **First person** (the default). The camera is at your eyes; you see your gun bottom-right and your melee weapon bottom-left (the *view model*), and a small crosshair in your colour. Shots go to whatever is under the crosshair. Mouse or right stick looks around; the left stick / WASD moves relative to where you look.
- **Top-down** (the original view). The camera floats above and behind, looking out to sea. Aim with the mouse cursor or the right stick; a ring in your colour shows the exact point you're aiming at. Movement is relative to the screen.
- Switch any time with **View** (R3 / Select on a pad, V on the keyboard) or from the pause menu. Each player picks their own view, so a split screen can mix them. The default view for new players is a setting.
- In first person your own body is hidden from your camera (other players still see it), and name tags float over teammates.
- Controllers get gentle aim assist in both views (slow-down over enemies plus a small nudge onto bodies in first person; snap to nearby bodies top-down). It can be turned off.

## Phases (data-driven)
Phases live in `Assets/Resources/Data/PhaseCycle.asset` as a list. The game runs whatever is listed, in order, looping. **Add, remove or reorder entries to change the day**; no code changes are needed. Each phase sets:

| Field | Meaning |
|---|---|
| `durationSeconds` | Ends after this long (0 = no timer) |
| `endWhenAllReady` | Ends when every player has rung the bell |
| `endWhenWavesCleared` | Ends when every planned enemy has spawned and died |
| `advancesDay` | The day counter goes up when this phase ends |
| `saveOnStart` | Autosave when the phase starts; players can also save from the pause menu during it |
| `shopOpen` | The shop and the fishmonger trade during this phase |
| `waves`, `budgetMultiplier`, `enemyPool` | How much the sea sends, and which creatures |
| `bossEveryDays`, `bossId` | Every Nth day this phase also brings the boss |
| `spawnFood`, `spawnGasCans` | Picnic leftovers and gas cans appear on the beach |
| `skyColor`, `sun*`, `ambient`, `fog*`, `torchesLit`, `lanterns` | The look; blended over 4 s between phases |

Any ticked end condition ends the phase. A phase with none ends after 60 s, with a warning.

The shipped day:

| Phase | Ends | What happens |
|---|---|---|
| **Morning** | all ready, or 150 s | Autosave; shop + fishmonger open; picnic food and gas cans appear; warm low sun |
| **Evening** | waves cleared | 2 waves at 55% budget from a lighter pool; orange sunset; torches lit |
| **Night** | waves cleared → day +1 | 3 waves at full budget; Glizzy Monster boss every 5th day; moonlight, torches, player lanterns |

**Waves:** the day's budget is `a + b·d + c·d²` (d = day) times the phase multiplier, spent on packs. Same rule as the 2D game: **once half of a wave is dead, the next one arrives 3 s later**.

## The playfield
A straight beach seen from above and behind (cameras look out to sea). The **town** (shop, fishmonger, hotdog stand, ready bell) is behind a stone **sea wall** with two gaps players can walk through. In front is dry sand, then wet sand, then the **sea**, whose floor slopes away. Enemies spawn on the seabed and walk out of the water. Ground enemies can't cross the wall; they attack it, or anyone on the beach. Gulls fly over it.

## Wetness
- Anything in the sea is **soaked** (wet = 1). On land it dries at `wetDrainPerSecond`.
- **Drips are proportional to wetness**: a soaked crab streams water, a half-dry one drips occasionally, a dry one doesn't. Wet creatures also look glossy.
- **Shock** (Boat Hook) does ×1.5 to wet targets and **chains** to up to two nearby wet ones.
- **Fire** (Flare Gun, gas cans) on a wet target just makes steam and dries it; on a dry target it burns (damage over time). The sea puts fires out.
- Players get wet too: jellyfish stings hurt more if you're standing in the water.

## Limbs ("shoot them where it hurts")
Some enemies have severable limbs. A hit lands on whatever is under your reticle; melee lands on the limb nearest the blade. Each limb has its own HP (a fraction of the enemy's). When it runs out, the limb **flies off** (physics) and the enemy changes:

| Effect | Who | Result |
|---|---|---|
| Kill (weak point, crit ×2–2.5) | Crab/hermit eyes, drowner/eel/turtle/croc/lobster head | Dies |
| Weaken attack | Crab/hermit/lobster claws, drowner arms | Damage halves per limb |
| Slow | Drowner legs, shark fin & tail, eel tail, octopus tentacles | Slower (octopus: −12% per tentacle; no ink below 3, no grab below 2) |
| Ground | Gull wings | Falls out of the sky and crawls |
| No tail | Croc tail | No more tail swipe |
| Shell off | Hermit shell | Loses its armour, panics, gets faster |
| Weaken sting | Jellyfish tentacles | −25% sting each |

**Corpse value** = base × (0.5 + 0.5 × intact limbs / all limbs). Blowing everything off is safe but cheap; a clean weak-point kill pays the most. That's the trade-off.

## Enemies
| Enemy | Asks | Answer | Limbs |
|---|---|---|---|
| **Crabling** | Many small, fast targets | Scattergun, oar sweep | Claws, eyes |
| **Drowner** | Baseline; can you hit heads? | Revolver on the head | Arms, legs, head |
| **Gull** | Flyers ignore the wall | Shoot the wings | Wings |
| **Hermit Crab** | Armour (4) | Shell off with the anchor, then anything | Shell, claws, eyes |
| **Puffer** | Kill it before it reaches you, or it explodes | Range; pop it inside a crowd | — |
| **Sea Urchin** | Punishes melee (spines hurt you) | Shoot it | — |
| **Jellyfish** | Stings everyone close, worse in water | Stay on dry sand; cut tentacles | 4 tentacles |
| **Shark** | Hunts anyone in the water; beaches itself if nobody's swimming and flops at the wall | Stay out of the sea; take the tail | Fin, tail |
| **Eel** | Fast leaping lunges | Spread; the tail stops the lunges | Head, tail |
| **Shellback** | Big armour (6), slow | Harpoon (pierce ignores armour), anchor | Head |
| **Saltwater Croc** | Grabs and death-rolls a player; tail-swipes anyone behind it | Teammates hit it to break the grab; cut the tail | Head, tail |
| **Octopus** | Grabs from 3.5 m and pulls; inks your screen | Cut tentacles | 6 tentacles |
| **Clawback** (lobster) | Heavy damage, armour | Claws first, then the head | Claws, head |
| **Glizzy Monster** (boss) | Huge, summons crabs, shakes the screen | Everyone, everything | — |

Bosses arrive on nights where `day % bossEveryDays == 0` (day 5, 10, 15…).

## Players
- **Health 100** (+ Sea Legs upgrades). At 0 you're **down**: you crawl and bleed out over 30 s. A teammate holding Interact next to you for 2 s revives you at 40%. Bleed out and you're back at the start of the next phase. Everyone down at once = game over.
- **Dash** (short burst, 0.8 s cooldown). Carrying weight and wading slow you.
- **Carry**: pick up corpses and gas cans, stacked up to weight 3 (crab 1 · drowner 2 · shark 3). **Fire throws** the top item toward your reticle. A corpse thrown into the fishmonger's cart sells itself.
- **Grabbed** (croc/octopus): mash any button to break free, or get a teammate to hit the grabber.

### Guns (each player's own; bought with team coins)
| Gun | Price | Notes |
|---|---|---|
| Revolver | start | 22 dmg, 6 rounds, good on weak points |
| Scattergun | 300 | 7 × 9, wide, short range |
| Harpoon Gun | 600 | 60 dmg, pierces everything (ignores armour), knockback into the surf |
| Flare Gun | 450 | Fire, small splash; steams wet targets |

### Melee (nautical and beach)
| Weapon | Price | Notes |
|---|---|---|
| Beach Spade | start | 18 dmg, 100° arc |
| Cutlass | 250 | Fast, best for lopping limbs |
| Oar | 300 | 160° sweep, big knockback |
| Boat Hook | 400 | 3.4 m reach, pulls targets in, Shock (chains through wet ones) |
| Anchor | 700 | 70 dmg, stuns, slows you while swinging |

### Team upgrades (shop)
Repair Wall (+30%), Reinforce Wall (+20% max), Sea Legs (+25 HP each), Heavier Rounds (+15% gun damage), Whetstone (+15% melee), Deep Pockets (+30% magazine), Gas Delivery (+2 gas cans each morning).

## Morning economy
- **Coins are shared.** Kills give a small bounty; the real money is **selling corpses** to the fishmonger in the morning (carry them, or throw them into the cart).
- **The shop** sells guns, melee weapons and team upgrades (morning only).
- **Picnic food** (burgers, fries, sodas, donuts) appears on the beach each morning, free to eat.

## Hotdog stand (on two tires)
The stand is a cart on two of the website's tire models. It's open every phase, so you can heal mid-fight if you can get back to town.

| Item | Price | Effect |
|---|---|---|
| Hotdog | 10 | +35 HP |
| + **Ketchup** | +4 | **Rage**: +50% damage for 20 s (coat flashes red) |
| + **Mustard** | +4 | **Speed**: +40% move speed for 20 s |
| + **??? Sauce** | +6 | A gamble (below) |
| Burger | 16 | +60 HP |
| Fries | 8 | +20 HP, speed for 8 s |
| Soda | 6 | +10 HP, washes off ink, slow and burns |
| Donut | 8 | +25 HP, Sugar Rush: +25% fire rate for 15 s |

### ??? Sauce
One roll from a weighted table:
- **Good:** Giant (+25% damage, takes less), Invincible 8 s, Double Damage 15 s, full heal, 25–150 coins.
- **Bad:** Upside-down camera, reversed controls, Tiny, Fisheye, Slow, **wrong view** (swaps you to the other POV: first person ↔ top-down), ink, tunnel vision.
- **Neutral:** Rainbow coat, Dutch-tilted camera, bubbles.
- **Jackpot:** a **0.01% chance** (`sauceJackpotChance = 0.0001`) of **10,000 coins**.

## Gas cans ("may explode")
Gas cans are dropped on the beach each morning (3, plus Gas Delivery upgrades).
- A bullet or melee hit has a **60%** chance to blow one. Otherwise it **leaks**, and the next hit always blows it.
- **Fire always** sets one off.
- Throwing one: **70%** chance it explodes when it lands.

An explosion does 90 damage in 4.5 m, sets things alight, **hurts players too**, chains into other cans, and shakes nearby cameras.

## Controls
Defaults (every one except the sticks and mouse look can be rebound in **Settings → Controls**):

| Action | Gamepad | Keyboard + mouse |
|---|---|---|
| Join | any button | any key |
| Move | left stick / d-pad | WASD / arrows |
| Look (first person) / aim (top-down) | right stick | mouse |
| Shoot / throw | RT | left click |
| Melee | RB | right click / F |
| Interact (shop, sell, eat, revive, pick up) | A / Cross | E / Enter |
| Back (close menu) | B / Circle | Backspace / Tab |
| Reload | X / Square | R |
| Swap gun | Y / Triangle | Q / middle click |
| Dash | LB / LT | Space / Shift |
| Switch view | R3 / Select | V |
| Pause menu | Start | Esc / P |

In menus: up/down chooses, left/right changes a value (sliders, toggles), Interact picks, Back goes back. HUD prompts always show *your* key for the action, including after rebinding.

Split screen: 1 player full screen · 2 players stacked · 3 players in quarters (the 4th quarter shows an overview and a join prompt) · 4 players in quarters. Wide, short viewports (two players stacked) pull the top-down camera back and look further out to sea, so each player still sees the beach. The mouse is captured while a keyboard player looks around in first person and released in menus.

## Main menu
A still picture from the upper room of the **keep** behind the town, through its big arched window: the town, the wall, the morning beach with a beach-volleyball game, umbrellas, gulls overhead and shark fins circling offshore; torches and a map table in the room. It's the 3D version of the 2D game's shop screen.

The menu panel on the left: **Continue** (latest save) · **New game** (pick a slot) · **Load game** · **Host online game** · **Join online game** · **Settings** · **Delete a save** · **Quit**. Mouse (hover and click, wheel), keyboard or any controller. Whoever picks the game becomes player 1; others drop in with any button as usual.

## Pause menu and settings
**Start / Esc** opens the pause menu in your own viewport: Resume · Switch view · Settings · Save game · Quit to main menu · Quit to desktop. Offline, the game pauses while anyone has it open; online the beach keeps going (stay near the wall).

| Settings | |
|---|---|
| **Audio** | Master, Effects (guns, creatures, explosions), Interface (menus, bells, coins), Ambience (surf) |
| **Game** | Start in first person · Field of view · Mouse sensitivity · Stick sensitivity · Invert look · Aim assist · Screen shake · Fullscreen · Graphics quality · Show frame rate |
| **Controls** | Keyboard & mouse and Controller lists: pick an action, press the new key or button (Esc cancels). Reset all controls. |

Settings are per machine (not per save) and apply instantly.

## Saves
- Three **slots**. A new game picks a slot; the run then **autosaves at the start of every Morning** (any phase with `saveOnStart`).
- During that phase you can also **Save game** from the pause menu (after shopping, say). Quitting to the menu during it saves first.
- A save holds the day, coins, wall HP, team upgrades and each player slot's guns and melee weapons. Corpses, food and statuses aren't kept: you always resume at the start of a calm phase.
- When the wall falls, **Try again** reloads the last save (or starts over if the run wasn't saved).
- Online, the host's save is used; clients don't save.

## Online
- **Host:** Main menu → Host online game → new or saved game. The host plays as normal; friends join any time. The pause menu shows the host's addresses.
- **Join:** Main menu → Join online game. Games on the same network appear in the list; otherwise type the host's address. Whoever picked Join becomes that machine's first player; more people on that machine press a button to drop in (couch + online mix, 4 players in total).
- Internet play works by address if the host forwards TCP port 7777 (changeable in the Host menu) on their router.
- The host runs the beach; everyone else sees it a moment later. Your own movement and looking are instant on your machine.
- If the host leaves, everyone returns to the main menu. If a client leaves, their sailor leaves with them (their carried items drop).

## Adding content
For a **new enemy**:
1. Add its model to `Blender/generate_models.py`. Severable parts are `Limb_<Part>` with the pivot at the joint, front facing -Y. Re-run the script.
2. Add an `EnemyDef` to `GameDatabase.FillDefaults()` (and to the asset). Choose an `EnemyBehaviour`, or add a new one plus a method in `Enemy.cs`.
3. Give every limb a `LimbDef` and pick its effect.
4. Answer the design questions: what does it **ask** of the players, what **counters** it, how does it interact with **wetness**, what's its corpse worth?
5. Add it to a phase's `enemyPool` if it should be phase-limited, and to the bestiary capture.
6. Online: it's replicated automatically (`Enemy.Spawn` tags it). If it animates its model root or has new visual state, make sure `Enemy.WriteNet`/`ApplyNet` carry it.
7. Run the tests and the capture tour.

For a **new phase**: add a `PhaseDefinition` to the cycle. If it needs behaviour no field covers, add a field (default off) and handle it in `PhaseManager`, `WaveDirector` or `World`. Never branch on the phase id (R2).
