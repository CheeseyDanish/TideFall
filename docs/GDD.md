# TideWall 3D — Game Design Document

## Pitch
A co-op **roguelike** for up to four friends, on one screen or online, in first person or from above. Behind a crumbling sea wall stands the town's **Nuclear Sand Powerplant**; if the creatures of the sea reach it and it melts down, the run is over. Every **morning** you sell the night's catch to the fishmonger, buy guns and upgrades, grab a hotdog (choose your condiment carefully) and spend your level-up points. Every **evening** and **night**, the team votes on a **tidal card** and the sea sends crabs, sharks, saltwater crocs, octopuses and worse **riding the waves** onto a long beach. Shoot their claws off, hook them with a boat hook, swing an anchor, scoop up the **sand dollars** they drop. Then do it all again, one day harder, until the core goes critical.

It's the 3D, co-op follow-up to Tidewall 2D (the browser game on the website), keeping its central idea: **the beach is a gradient from wet to dry**, and wetness changes how fights go.

## Core loop
```
             ┌──────────────────────────────── one day ────────────────────────────────┐
 Main menu ─► MORNING ──(everyone rings the bell / timer)─► EVENING ──(waves cleared)─► NIGHT ──(waves cleared, day +1)─┐
  new run /   autosave, shop, fishmonger, hotdogs, stats    tidal card vote,             tidal card vote, 3 waves,       │
  continue /  & mods, picnic food, gas cans                 2 light waves                boss every 5th day              │
  host/join  ▲───────────────────────────────────────────────────────────────────────────────────────────────────────────┘
 Lose: the powerplant melts down, or every player is down at once → the run ends (its save is deleted). New run, or menu.
```

## A roguelike run
- **A run lasts until the powerplant melts down** (or everyone is down at once). Then it's over: the run's save slot is deleted and the **Legacy** record (runs, best day, best level, total kills) is updated and shown on the main menu.
- Runs are **suspendable**: they autosave every Morning, so you can quit and Continue later, but you can't reload to undo a loss.
- Each run is different: which **tidal cards** you're offered and vote for, which **weapon mods** you're offered, how you spend **stat points**, and what the waves bring.

## Views
- **First person** (the default). The camera is at your eyes; you see your gun bottom-right (and your melee weapon sweeping across when you swing), and a small crosshair in your colour. Shots go to whatever is under the crosshair. Mouse or right stick looks around; the left stick / WASD moves relative to where you look.
- **Top-down** (the original view). The camera floats above and behind, looking out to sea. Aim with the mouse cursor or the right stick; a ring in your colour shows the exact point you're aiming at.
- Switch any time with **View** (R3, V) or from the pause menu. Each player picks their own view. The default view for new players is a setting.
- In first person your own body is hidden from your camera, and name tags float over teammates. Controllers get gentle aim assist in both views (it can be turned off).

## The playfield
The beach is **twice the size of the original**: 88 m of wall, 40 m of sand to the waterline.
```
 keep ── town (Nuclear Sand Powerplant, shop, fishmonger, hotdog stand, ready bell) ── SEA WALL ── dry sand ── wet sand ── sea
```
- **The keep** stands behind the town; its upper room is the main menu's view.
- **The Nuclear Sand Powerplant** (east side of town) is a sandcastle reactor: a dome with a radiation sign, two cooling towers that steam (greener as it overheats) and a glowing green core that flickers red when critical. **1000 HP**. At 30% an alarm sounds; at 0 it **melts down**.
- **The sea wall** is made of 20 **sections**, each with its own HP (140). Creatures attack the section in front of them. A section at 0 HP is **breached**: it slumps into rubble and stops blocking. Creatures head for the nearest breach, walk through it and go for the powerplant (and anyone in town). Two **sally ports** (gaps players can walk through, creatures can't) sit at x = ±22.
- **Repair Wall** in the shop restores every section and **rebuilds breached ones**.

## Waves and breakers
- Creatures **arrive riding breaking waves**. Spawns that come due within ~1.5 s of each other climb onto the same breaker far out at sea; it rolls in (the sea swells under it), rears up near the shore, and **breaks**, throwing them into the surf soaking wet. Bosses get a monster wave of their own. Gulls fly in over the water.
- The day's budget is `a + b·d + c·d²` (d = day) times the phase multiplier, spent on packs and split across waves. **Once half of a wave is dead, the next one arrives 3 s later.**
- Active tidal cards scale the numbers as spawns come due (some creatures never come, or bring a friend).

## Phases (data-driven)
Phases live in `Assets/Resources/Data/PhaseCycle.asset` as a list, run in order, looping. **Add, remove or reorder entries to change the day**; no code changes. Each phase sets:

| Field | Meaning |
|---|---|
| `durationSeconds` | Ends after this long (0 = no timer) |
| `endWhenAllReady` | Ends when every player has rung the bell |
| `endWhenWavesCleared` | Ends when every planned enemy has spawned and died |
| `advancesDay` | The day counter goes up when this phase ends |
| `saveOnStart` | Autosave when the phase starts; players can also save from the pause menu during it |
| `tidalDraft` | The team votes on a tidal card when the phase starts |
| `shopOpen` | The shop and the fishmonger trade during this phase |
| `waves`, `budgetMultiplier`, `enemyPool` | How much the sea sends, and which creatures |
| `bossEveryDays`, `bossId` | Every Nth day this phase also brings the boss |
| `spawnFood`, `spawnGasCans` | Picnic leftovers and gas cans appear on the beach |
| `skyColor`, `sun*`, `ambient`, `fog*`, `torchesLit`, `lanterns` | The look (procedural sky, sun, fog); blended over 4 s |

The shipped day:

| Phase | Ends | What happens |
|---|---|---|
| **Morning** | all ready, or 150 s | Autosave; shop + fishmonger open; picnic food and gas cans; warm low sun |
| **Evening** | waves cleared | Tidal card vote; 2 waves at 55% budget from a lighter pool; orange sunset; torches lit |
| **Night** | waves cleared → day +1 | Tidal card vote; 3 waves at full budget; Glizzy Monster boss every 5th day; moonlight, torches, lanterns |

## Tidal cards
At the start of a `tidalDraft` phase the team is offered **3 cards** and **votes** (each player in their own view; Stats button to vote later). Ties and no-shows are decided at random when the 15 s timer runs out. A card **lasts several waves**, carrying over into later phases; several can be active at once (shown top-right).

| Card | Waves | Effect |
|---|---|---|
| Red Tide | 3 | Creatures +40% HP. Sand dollars +60% |
| King Tide | 2 | Breakers carry creatures far up the beach. +50% XP |
| Riptide | 3 | Creatures 25% faster; you're 15% faster |
| Downpour | 3 | Everything stays soaked (shock hits hard, fire fizzles); light fog; sand dollars +30% |
| Fog Bank | 2 | Thick fog. Creatures −20% HP |
| Feeding Frenzy | 3 | 30% more creatures. Corpses sell for 50% more |
| Calm Waters | 2 | 30% fewer creatures, 30% less reward (the safe pick) |
| Glass Cannon | 3 | +50% damage, −40% max HP |
| Gold Rush | 2 | Double sand dollars, but they wash away after 8 s |
| Plankton Bloom | 3 | Double XP; creatures hit 20% harder |

**Exclusive cards** only appear in their kind of game, and do things nothing else does:

| Card | Only in | Waves | Effect |
|---|---|---|---|
| **Buddy Beam** | Online (players on 2+ computers) | 3 | A cyan laser links teammates on *different computers* (up to 30 m apart). Creatures crossing it burn. 20% more creatures |
| **Message in a Bottle** | Online | 3 | Every wave, each player gets a condiment boost "sent" by a teammate on another computer |
| **Screen Swap** | Split screen (2+ people on one screen) | 2 | Every 20 s the viewports on each screen shuffle. +40% damage for everyone sharing a screen |
| **Shared Snacks** | Split screen | 3 | Food heals everyone on your screen; reviving a screen-mate is twice as fast |

When exclusive cards are possible, one of the three choices is exclusive 60% of the time.

## Levels, stats and weapon mods
- **XP**: the killer gets a creature's XP (10 + 12 × its cost); everyone else on the team gets half. XP for level L+1 = 60 × L^1.35.
- **Each level**: +1 stat point, a small heal, a toast. Spend points in the **Stats & Mods** menu (Stats button: Select / Tab or C; also in the pause menu).

| Stat | Per point (max 10) |
|---|---|
| Vigor | +12 max HP |
| Brawn | +8% melee damage |
| Aim | +6% gun damage |
| Swiftness | +4% move speed, faster dash cooldown |
| Quick Hands | +7% reload speed, +3.5% fire rate |
| Fortune | +8% sand dollars you pick up, +4% XP |

- **Respec**: take back every point to spend again. Costs 15 × your level sand dollars, more each time (×2, ×3…), from the team purse.
- **Weapon mods**: every **5 levels** you're offered **3 mods** for the gun and melee weapon in your hands; pick one. A mod stays on that weapon (swap away and back, it's still there).

| Gun mods | | Melee mods | |
|---|---|---|---|
| Extended Mag | +50% magazine | Serrated Edge | +60% damage to limbs |
| Hair Trigger | +25% fire rate | Barnacle-Crusted | hits stun 0.5 s |
| Speed Loader | reload 30% faster | Riptide Edge | big knockback |
| Barbed Rounds | +1 pierce | Long Handle | +30% reach |
| Incendiary Rounds | fire element | Wide Swing | +35% arc |
| Live Wire | shock element | Heavy Head | +35% damage, 20% slower |
| Blast Caps | small splash | Eel-Skin Grip | swings 25% faster |
| Heavy Slugs | +30% damage, −15% fire rate | Jellyfish Coil | shock element |
| Leech Rounds | heal 8% of damage | Vampire Squid | heal 12% of damage |
| Buckshot | +2 pellets | | |

## Sand dollars (the money)
- The team shares one purse of **sand dollars** (shown with a sand dollar icon top-right).
- Creatures **drop sand dollars** when they die (their bounty × tidal cards). They scatter on the sand and **slide to any player within 4.5 m**; your Fortune adds to what you pick up. Uncollected ones wash away after 30 s (sooner in the surf).
- The real money is still **selling corpses** to the fishmonger in the morning (carry them, or throw them into the cart). Intact corpses are worth more.

## Wetness
- Anything in the sea (or riding a breaker) is **soaked** (wet = 1). On land it dries at `wetDrainPerSecond`.
- **Drips are proportional to wetness**: a soaked crab streams water, a half-dry one drips occasionally, a dry one doesn't. Wet creatures look glossy.
- **Shock** does ×1.5 to wet targets and **chains** to up to two nearby wet ones. **Fire** on a wet target just steams it dry; on a dry target it burns.

## Limbs ("shoot them where it hurts")
Some enemies have severable limbs. A hit lands on whatever is under your crosshair/reticle; melee lands on the limb nearest the blade. Each limb has its own HP (a fraction of the enemy's). When it runs out, the limb **flies off** (physics) and the enemy changes:

| Effect | Who | Result |
|---|---|---|
| Kill (weak point, crit ×2–2.5) | Crab/hermit eyes, drowner/eel/turtle/croc/lobster head | Dies |
| Weaken attack | Crab/hermit/lobster claws, drowner arms | Damage halves per limb |
| Slow | Drowner legs, shark fin & tail, eel tail, octopus tentacles | Slower (octopus: −12% per tentacle; no ink below 3, no grab below 2) |
| Ground | Gull wings | Falls out of the sky and crawls |
| No tail | Croc tail | No more tail swipe |
| Shell off | Hermit shell | Loses its armour, panics, gets faster |
| Weaken sting | Jellyfish tentacles | −25% sting each |

**Corpse value** = base × (0.5 + 0.5 × intact limbs / all limbs).

## Enemies
| Enemy | Asks | Answer | How it moves |
|---|---|---|---|
| **Crabling** | Many small, fast targets | Scattergun, oar sweep | Scuttles **sideways** on tiptoe, claws snapping, eye stalks looking about |
| **Drowner** | Baseline; can you hit heads? | Revolver on the head | Shambles, arms out in front, head lolling |
| **Gull** | Flies over the wall, straight for the powerplant | Shoot the wings | Glides and banks; grounded, it hops |
| **Hermit Crab** | Armour (4) | Shell off with the anchor | Scuttles sideways, shell wobbling |
| **Puffer** | Explodes when it reaches you (or the wall) | Range; pop it in a crowd | Bobs along, then **puffs into a quivering, spinning spiky ball** as it closes in |
| **Sea Urchin** | Punishes melee (spines) | Shoot it | Rolls on its spines, bristling |
| **Jellyfish** | Stings everyone close, worse in water | Stay on dry sand; cut tentacles | Bell pulses, streamers trail |
| **Shark** | Hunts swimmers; beaches itself and flops at the wall | Stay out of the sea; take the tail | Sweeping S-swim; flops furiously on sand |
| **Eel** | Fast leaping lunges | Spread; the tail stops the lunges | Slithers, leaps |
| **Shellback** | Big armour (6), slow | Harpoon, anchor | Heavy plod, slowly looking around |
| **Saltwater Croc** | Grabs and death-rolls a player; tail-swipes | Teammates break the grab; cut the tail | Low belly waddle, swinging tail, jaws up to bite, death roll |
| **Octopus** | Grabs from 3.5 m and pulls; inks your screen | Cut tentacles | Mantle breathes, tentacles curl |
| **Clawback** (lobster) | Heavy damage, armour | Claws first, then the head | Walks forward, rears up to strike |
| **Glizzy Monster** (boss) | Huge, summons crabs, shakes the screen | Everyone, everything | Bounces and squashes |

Behind a breach, creatures go for the powerplant and anyone in town. Bosses arrive on nights where `day % bossEveryDays == 0`.

## Players
- **Health 100** (+ Sea Legs, + Vigor). At 0 you're **down**: you crawl and bleed out over 30 s. A teammate holding Interact next to you for 2 s revives you at 40%. Bleed out and you're back at the start of the next phase.
- **Dash** (short burst). Carrying weight and wading slow you.
- **Carry**: pick up corpses and gas cans, stacked up to weight 3. **Fire throws** the top item. A corpse thrown into the fishmonger's cart sells itself.
- **Grabbed** (croc/octopus): mash any button to break free, or get a teammate to hit the grabber.

### Guns (each player's own; bought with team sand dollars)
| Gun | Price | Notes |
|---|---|---|
| Revolver | start | 22 dmg, 6 rounds, good on weak points |
| Scattergun | 300 | 7 × 9, wide, short range |
| Harpoon Gun | 600 | 60 dmg, pierces everything (ignores armour), knockback |
| Flare Gun | 450 | Fire, small splash; steams wet targets |

### Melee (nautical and beach)
| Weapon | Price | Notes |
|---|---|---|
| Beach Spade | start | 18 dmg, 100° arc |
| Cutlass | 250 | Fast, best for lopping limbs |
| Oar | 300 | 160° sweep, big knockback |
| Boat Hook | 400 | 3.4 m reach, pulls targets in, Shock |
| Anchor | 700 | 70 dmg, stuns, slows you while swinging |

### Team upgrades (shop)
Repair Wall (every section +40%, breaches rebuilt) · Reinforce Wall (+20% section HP) · **Cool the Core** (+25% powerplant HP) · **Lead Lining** (+20% powerplant max HP) · Sea Legs (+25 HP each) · Heavier Rounds (+15% gun damage) · Whetstone (+15% melee) · Deep Pockets (+30% magazine) · Gas Delivery (+2 gas cans each morning).

## Hotdog stand (on two tires)
Open every phase, so you can heal mid-fight if you can get back to town.

| Item | Price (sand dollars) | Effect |
|---|---|---|
| Hotdog | 10 | +35 HP |
| + **Ketchup** | +4 | **Rage**: +50% damage for 20 s |
| + **Mustard** | +4 | **Speed**: +40% move speed for 20 s |
| + **??? Sauce** | +6 | A gamble (below) |
| Burger | 16 | +60 HP |
| Fries | 8 | +20 HP, speed for 8 s |
| Soda | 6 | +10 HP, washes off ink, slow and burns |
| Donut | 8 | +25 HP, Sugar Rush: +25% fire rate for 15 s |

### ??? Sauce
- **Good:** Giant, Invincible 8 s, Double Damage 15 s, full heal, 25–150 sand dollars.
- **Bad:** Upside-down camera, reversed controls, Tiny, Fisheye, Slow, **wrong view** (swaps first person ↔ top-down), ink, tunnel vision.
- **Neutral:** Rainbow coat, Dutch-tilted camera, bubbles.
- **Jackpot:** a **0.01% chance** of **10,000 sand dollars**.

## Gas cans ("may explode")
Dropped on the beach each morning (3, plus Gas Delivery). A hit has a **60%** chance to blow one (otherwise it leaks, and the next hit always blows it); **fire always** sets one off; a thrown one explodes **70%** of the time. 90 damage in 4.5 m, sets things alight, hurts players too, chains into other cans.

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
| Back (close menu) | B / Circle | Backspace |
| Reload | X / Square | R |
| Swap gun | Y / Triangle | Q / middle click |
| Dash | LB / LT | Space / Shift |
| Switch view | R3 | V |
| **Stats & mods / tidal card vote** | Select / Back | Tab / C |
| Pause menu | Start | Esc / P |

In menus: up/down chooses, left/right changes a value, Interact picks, Back goes back. HUD prompts always show *your* key.

Split screen: 1 player full screen · 2 stacked · 3–4 in quarters (with 3, the 4th quarter is an overview). The mouse is captured while a keyboard player looks around in first person.

## HUD
Top-left: name, health, **level + XP bar** (unspent points and "MOD!" when one's waiting). Top-centre: phase, wave, creatures left, **powerplant bar** and **wall sections** (breaches in red), the tidal card vote timer. Top-right: **sand dollars** (icon + number) and active **tidal cards** with waves left. Bottom: weapons, ammo, prompts.

## Main menu
A still picture from the upper room of the **keep**, through its big arched window: the town, the wall, the morning beach with beach volleyball, umbrellas, sandcastles, gulls overhead and shark fins circling offshore; torches, a map table and a chest of sand dollars in the room. It's the 3D version of the 2D game's shop screen.

Menu: **Continue run** · **New run** (pick a slot) · **Other runs** · **Host online game** · **Join online game** · **Settings** · **Delete a save** · **Quit**, plus the Legacy record. Mouse, keyboard or any controller; whoever picks becomes player 1.

## Pause menu and settings
**Start / Esc**: Resume · Switch view · **Stats & mods** · Settings · Save game · Quit to main menu · Quit to desktop. Offline, the game pauses while anyone has it open; online the beach keeps going.

| Settings | |
|---|---|
| **Audio** | Master, Effects, Interface, Ambience (surf) |
| **Game** | Start in first person · Field of view · Mouse/stick sensitivity · Invert look · Aim assist · Screen shake · Fullscreen · Graphics quality · Show frame rate |
| **Controls** | Keyboard & mouse and Controller lists: pick an action, press the new key or button. Reset all controls. |

## Saves
- Three **slots**, one run each. A run **autosaves every Morning** (`saveOnStart`), and can be saved from the pause menu then. Quitting to the menu during it saves first.
- A save holds the day, sand dollars, every wall section, the powerplant, team upgrades, active tidal cards, and each player slot's weapons, **level, XP, stat points, mods** and respec count.
- When the run ends, its save is **deleted** (roguelike).

## Online
- **Host:** Main menu → Host online game → new or saved run. Friends join any time; the pause menu shows the host's addresses.
- **Join:** Games on the same network are listed; otherwise type the host's address. Several people on one machine can join (couch + online, 4 players in total).
- Internet play needs TCP port 7777 forwarded on the host's router.
- The host runs everything (creatures, breakers, sand dollars, cards, levels); your own movement and looking are instant on your machine.

## Adding content
For a **new enemy**:
1. Add its model to `Blender/generate_models.py` (`Limb_<Part>` with the pivot at the joint, front facing -Y). Re-run the script.
2. Add an `EnemyDef` to `GameDatabase.FillDefaults()` (and the asset). Choose an `EnemyBehaviour`, or add one plus a method in `Enemy.cs`.
3. Give every limb a `LimbDef`, and give it **its own animation** in `Enemy.PoseRoot` / `AnimateLimbs`: every creature moves in a way you can recognise.
4. Answer the design questions: what does it **ask**, what **counters** it, how does it interact with **wetness**, what's its corpse worth, what does it do **behind a breach**?
5. Add it to a phase's `enemyPool` if needed, and to the bestiary capture. Run the tests and the capture tour.

For a **new tidal card**: add a `TidalCardDef` (data only, if its effect is one of the existing fields). An exclusive card needs a `special` with a behaviour that *only makes sense* in its mode, handled in `TidalCards.cs`.

For a **new stat or mod**: add a `StatDef` (pick a `StatKind`) or a `ModDef` (multipliers/additions on the weapon). New kinds of effect need code in `Player` (`StatBonus` users, `RebuildWeapons`).

For a **new phase**: add a `PhaseDefinition`. If it needs behaviour no field covers, add a field (default off) and handle it in `PhaseManager`, `WaveDirector` or `World`. Never branch on the phase id (R2).
