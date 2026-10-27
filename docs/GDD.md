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

## Melee first
The run's **first 3 waves are melee only**: guns stay holstered (`gunsUnlockWave = 4`), and the trigger swings your melee weapon instead. Learn the creatures up close first. The HUD says when guns come out.

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
- **The sea wall** is made of 20 **sections**, each with its own HP (140). Creatures attack the section in front of them. It looks like **one unbroken wall** (running-bond masonry on both faces, battlements every 2 m). A section at 0 HP is **breached**: it collapses into a heap of rubble, the sections either side show broken ends, and the gap stops blocking. Creatures head for the nearest breach, walk through it and go for the powerplant (and anyone in town). Two **sally ports** (gaps players can walk through, creatures can't) sit at x = ±22, each under a **gatehouse** arch (tall enough for a Giant) with red banners, so the wall stays one piece.
- **Repair Wall** in the shop restores every section and **rebuilds breached ones**.

## Waves and breakers
- Creatures **arrive riding breaking waves**, standing on little **surfboards**, arms out, wobbling for balance. About **1 in 6 wipes out** on the way in: off the board, cartwheeling into the surf, board flying. The wave itself curls over into a foaming lip that throws spray. Spawns that come due within ~1.5 s of each other climb onto the same breaker far out at sea; it rolls in (the sea swells under it), rears up near the shore, and **breaks**, throwing them into the surf soaking wet. Bosses get a monster wave of their own. Gulls fly in over the water.
- The day's budget is `a + b·d + c·d²` (d = day) times the phase multiplier, spent on packs and split across waves. **Once half of a wave is dead, the next one arrives 3 s later.**
- Active tidal cards scale the numbers as spawns come due (some creatures never come, or bring a friend).
- **Run waves** count across the whole run. **Gulls don't appear before wave 5**; giant clams are rare from the start.
- **What's next**: the HUD shows the rest of the current wave and the next wave ("Incoming: 4 Crabling, 2 Drowner · Wave 2: …"); in calm phases, what the next fight may bring.
- **Morning thieves**: a few gulls fly in each morning (`corpseThieves`) to snatch the night's corpses before you sell them. Shoot one and it drops its prize.

## Phases (data-driven)
Phases live in `Assets/Content/Data/PhaseCycle.asset` as a list, run in order, looping. **Add, remove or reorder entries to change the day**; no code changes. Each phase sets:

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
| `corpseThieves` | How many gulls come to steal corpses |
| `skyColor`, `sun*`, `ambient`, `fog*`, `torchesLit`, `lanterns` | The look (procedural sky, sun, fog); blended over 4 s |

The shipped day:

| Phase | Ends | What happens |
|---|---|---|
| **Morning** | all ready, or 150 s | Autosave; shop + fishmonger open; picnic food and gas cans; warm low sun |
| **Evening** | waves cleared | Tidal card vote; 2 waves at 55% budget from a lighter pool; orange sunset; torches lit |
| **Night** | waves cleared → day +1 | Tidal card vote; 3 waves at full budget; Glizzy Monster boss every 5th day; moonlight, torches, lanterns |

## Tidal cards
At the start of a `tidalDraft` phase the team **votes** between **3 face-up cards** and a **face-down random card** ("???", which deals any card allowed right now). Rarely (15%) a **Re-roll** card is offered too: if it wins, three new cards are dealt and you vote again. The vote shows as a row of big cards in each player's view (d-pad / arrows to choose, Interact to vote; Stats to come back to it). Ties and no-shows are decided at random when the 15 s timer runs out. A card **lasts several waves**, carrying over into later phases; several can be active at once (shown top-right).

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

## Sand dollars and golden pearls
- The team shares one purse of **sand dollars** (sand dollar icon, top-right) and one of **golden pearls**.
- When a creature dies, its sand dollars burst out and **fly to whoever killed it**, low over the sand. **Anyone they pass within 1.6 m catches them first**: teammates can intercept ("Intercepted 4 sand dollars from Red!"). With no killer (fire, gas), they land and slide to the nearest player. Fortune adds to what you catch. Uncollected ones wash away after 30 s.
- **Golden pearls** only come from the rare **Giant Clam** (2 each) and the same flight rules apply. They pay for the best defences.
- The real money is still **selling corpses** to the fishmonger in the morning (carry them, or throw them into the cart). Intact corpses are worth more.

## Defences (build anywhere on the beach or in town)
Open the **Build** menu (d-pad down / B), or the shop's **Defences** tab, pick one, then place it where you aim: a see-through preview shows green (OK) or red (not here). Interact places it, Reload turns it 45°, Back cancels. Not in the sea, not on the wall, not on the powerplant or the stalls, not on top of each other.

| Defence | Cost | What it does |
|---|---|---|
| Sandbag Barricade | 40 SD | Blocks creatures; they have to chew through it (260 HP) |
| Urchin Spikes | 60 SD | Hurts anything walking over it (wears down as it does) |
| Kelp Snare | 50 SD | Half speed for anything crossing it |
| Bait Bucket | 30 SD | Creatures within 11 m go for the fish heads instead |
| Harpoon Turret | 120 SD + 1 pearl | Fires at the nearest creature (max 4) |
| Pearl Cannon | 200 SD + 3 pearls | Lobs exploding pearls into crowds (max 2) |

Defences are saved with the run.

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
| **Gull** (from wave 5) | Flies over the wall, straight for the powerplant; in the morning, steals corpses | Shoot the wings | Glides and banks; grounded, it hops |
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
| **Giant Clam** (rare) | Armour 10 while shut; drops **golden pearls** | Hit it when it gapes open (or knock the lid off) | Hops along shut; gapes open now and then and snaps |
| **Glizzy Monster** (boss) | Huge, summons crabs, shakes the screen | Everyone, everything | Bounces and squashes |

Behind a breach, creatures go for the powerplant and anyone in town. Bosses arrive on nights where `day % bossEveryDays == 0`.

## Players
- **The sailor is animated**: legs stride with your speed, the gun arm aims where you look, the other arm swings the melee weapon in its style, both arms go up to carry, the head follows your gaze, you lean into a dash and crawl when down.
- **Health 100** (+ Sea Legs, + Vigor). At 0 you're **down**: you crawl and bleed out over 30 s. A teammate holding Interact next to you for 2 s revives you at 40%. Bleed out and you're back at the start of the next phase.
- **Dash** (short burst). Carrying weight and wading slow you.
- **Carry**: pick up corpses and gas cans, stacked up to weight 3. In first person you pick up **what you're looking at** (within 4.5 m); otherwise the nearest thing. **Fire throws** the top item. A corpse thrown into the fishmonger's cart sells itself. Corpses lie on the sand (never sunk into it).
- **Grabbed** (croc/octopus): mash any button to break free, or get a teammate to hit the grabber.

### Gunplay
Every shot kicks: the view snaps up and settles (heavier guns kick harder), the gun jolts back, a muzzle flash and an ejected shell, the crosshair spreads and recovers. Hits flash the creature, make it flinch, and pop a **damage number** (yellow with "!" on weak points); kills flash a red X with a thunk.

### Guns (each player's own; bought with team sand dollars)
| Gun | Price | Notes | Reload minigame |
|---|---|---|---|
| Revolver | start | 22 dmg, 6 rounds, good on weak points | Rhythm |
| Scattergun | 300 | 7 × 9, wide, short range | Shell by shell |
| Rivet SMG | 400 | 9 dmg, 10/s, 24 rounds | Active (zone) |
| Flare Gun | 450 | Fire, small splash; steams wet targets | Drop |
| Harpoon Gun | 600 | 60 dmg, pierces everything (ignores armour), knockback | Heavy |
| Blowtorch | 700 | 4 × 5 fire, 9/s, 8 m range, 40 rounds | Pressure |
| Longrifle | 750 | 75 dmg, 60 m, goes through one | Bolt |
| Eel Caster | 850 | 18 shock, small splash, 10 rounds | Capacitor (mash) |

### Reloading (the 2D game's minigames)
Every gun has its own reload minigame, drawn **just under your reticle** (with what to press). Every one is **optional**: do nothing and the reload finishes in the gun's normal time. Skill makes it faster, and a **Perfect** reload adds **+25% damage** (Heavy: +40%) until that magazine is empty ("PERFECT +25%" by the reticle and the gun line). A miss **jams** (adds time).

| Style | How | Miss |
|---|---|---|
| Active (zone) | Tap Reload while the marker is in the green | Jam +0.6 s |
| Rhythm | Rounds load one at a time; tap Reload on each beat (a ring closes on the light) to snap one in. All six on the beat = Perfect | Fumble: a short stall |
| Shell by shell | Tap Reload as each shell's marker crosses the green. Fire to stop early with what's loaded | Slip: that shell takes longer |
| Bolt | Tap in the first zone (bolt open, skips ahead), then the second (bolt closed) | Jam +0.5 s |
| Heavy | One narrow gold zone on an accelerating marker. One chance | Jam +1.4 s |
| Drop | Like Active, but the zone is somewhere new every reload | Jam +0.6 s |
| Capacitor | Mash Reload to charge faster. Fast enough = Perfect | — |
| Pressure | Hold Reload: the needle rises; release in the green. Hold to the top and it vents | Vented +0.8 s |

When you're low on ammo the reticle shows what's left; when you're empty it says **RELOAD** with your button.

### Kill combos
Kills within **3 seconds** of each other chain into a **combo**, shown beside the reticle ("x4 FRENZY", then RAMPAGE at 6, UNSTOPPABLE at 10) with a bar showing the time left to keep it going. Every kill past the first pays **+1 sand dollar and +2 XP per step** (x4 → +3 SD), up to x20.

### Melee (nautical and beach)
Each weapon swings in its own style, with a wind-up, the strike (when the hit lands) and a follow-through, in first and third person:

| Weapon | Price | Style | Notes |
|---|---|---|---|
| Beach Spade | start | Chop: raised high, chopped down and across | 18 dmg, 100° arc |
| Cutlass | 250 | Slash: cocked back, a fast flat backhand with a wrist roll | Fast, best for lopping limbs |
| Oar | 300 | Sweep: a big low side-to-side sweep | 160°, big knockback |
| Boat Hook | 400 | Thrust: jab out, yank back | 3.4 m reach, pulls targets in, Shock |
| Anchor | 700 | Slam: heaved overhead, brought crashing down (long wind-up) | 70 dmg, stuns, kicks up sand |

### Team upgrades (shop)
Repair Wall (every section +40%, breaches rebuilt) · Reinforce Wall (+20% section HP) · **Cool the Core** (+25% powerplant HP) · **Lead Lining** (+20% powerplant max HP) · Sea Legs (+25 HP each) · Heavier Rounds (+15% gun damage) · Whetstone (+15% melee) · Deep Pockets (+30% magazine) · Gas Delivery (+2 gas cans each morning).

## The shop and the vendors
- **The shop** works like the 2D game's: tabs (**Guns**, **Melee**, **Upgrades**, **Defences**; Swap/Reload to switch) of **item cards** with a **picture of the item**, its stats, its price, and whether you own or have it equipped.
- **Vendors** stand behind the stalls: the **hotdog chef** (white coat, tall hat, moustache) and the **fishmonger** (yellow oilskins, sou'wester). They turn to whoever comes near, wave, and shout lines ("Ketchup for rage, mustard for speed!").
- **Menus never stop you walking**: the d-pad / arrow keys (and mouse wheel) drive menus while the left stick / WASD still walks and you can still look around. A stall's menu closes if you walk away from it.

## Hotdog stand (on two tires)
Open every phase, so you can heal mid-fight if you can get back to town. Its menu is a grid of food cards with pictures.

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
| Swap gun (shop: next tab) | Y / Triangle | Q / middle click |
| Dash | LB / LT | Space / Shift |
| Switch view | R3 | V |
| **Stats & mods / tidal card vote** | Select / Back | Tab / C |
| **Build defences** | D-pad down | B |
| **Menus: choose** | D-pad | Arrow keys / mouse wheel |
| Pause menu | Start | Esc / P |

In menus: the d-pad / arrows choose (left/right changes a value in lists), Interact picks, Back goes back. The left stick / WASD keeps walking. HUD prompts always show *your* key.

Split screen: 1 player full screen · 2 stacked · 3–4 in quarters (with 3, the 4th quarter is an overview). The mouse is captured while a keyboard player looks around in first person.

## HUD
Top-left: name, health, **level + XP bar** (unspent points and "MOD!" when one's waiting). Top-centre: phase, wave, creatures left, **powerplant bar** and **wall sections** (breaches in red), the tidal card vote timer. Top-right: **sand dollars** (icon + number) and active **tidal cards** with waves left. Bottom: weapons, ammo, prompts.

## Main menu
A still picture from the upper room of the **keep**, through its big arched window: the town, the wall, the morning beach with beach volleyball, umbrellas, sandcastles, gulls overhead and shark fins circling offshore; torches, a map table and a chest of sand dollars in the room. It's the 3D version of the 2D game's shop screen.

Menu: **Continue run** · **Part I · The Wall** (pick a slot) · **Part II · The Undertow** (pick a slot) · **Other runs** · **Host online game** (either part) · **Join online game** · **Settings** · **Delete a save** · **Quit**, plus the Legacy record and, once any are found, the Spiral Shards. Mouse, keyboard or any controller; whoever picks becomes player 1.

Low on the wall beside the window, someone has been keeping count: one scratch for every run you've played, in fives. Nobody mentions it.

## Pause menu and settings
**Start / Esc**: Resume · Switch view · **Stats & mods** · Settings · Save game · Quit to main menu · Quit to desktop. Offline, the game pauses while anyone has it open; online the beach keeps going.

| Settings | |
|---|---|
| **Audio** | Master, Effects, Interface, Ambience (surf) |
| **Game** | Start in first person · Field of view · Mouse/stick sensitivity · Invert look · Aim assist · Screen shake · Damage numbers · Show frame rate |
| **Graphics** | Display mode (borderless / exclusive fullscreen / windowed) · Resolution (native by default) · VSync · Frame rate limit · Render scale (50–200%) · Anti-aliasing (Off, FXAA, SMAA, MSAA 2/4/8x; MSAA 4x default) · Shadows (off/low/medium/high) · Bloom · Post-processing · Draw distance · Particle density · Quality preset |
| **Controls** | Keyboard & mouse and Controller lists: pick an action, press the new key or button. Reset all controls. |

## Saves
- Three **slots**, one run each. A run **autosaves every Morning** (`saveOnStart`), and can be saved from the pause menu then. Quitting to the menu during it saves first.
- A save holds the day, sand dollars, every wall section, the powerplant, team upgrades, active tidal cards, and each player slot's weapons, **level, XP, stat points, mods** and respec count.
- When the run ends, its save is **deleted** (roguelike).

## Online
- **Host:** Main menu → Host online game → new or saved run. Choose how friends join: a **join code** (anywhere on the internet; encrypted; no router setup) or **LAN / address**. Optionally set a **room password**. The pause menu shows the join code or your addresses.
- **Join:** Games on the same network are listed; otherwise type the join code (or the host's address). Several people on one machine can join (couch + online, 4 players in total).
- LAN / address games over the internet need UDP port 7777 forwarded on the host's router; join codes don't.
- The host runs everything (creatures, breakers, sand dollars, cards, levels, and everyone's movement); your own movement and looking still respond instantly on your machine, and the host gently corrects it if they disagree.

## Part II — The Undertow
The inverse of Part I. The engineers run the core hot enough to push the sea back for one night, and the expedition
marches out across the drained seabed to find the **Heart of the Tide** and end the war. Instead of holding a line you
push down a lane between two standing walls of sea; instead of waves arriving, creatures step out of the water walls
ahead of you. A **tide clock** (the overdriven core) ends the run if it runs out.

Five stages, each its own scene, played in order. The team, its sand dollars, pearls, upgrades and levels travel
between them, and the run saves at every gate. New sailors start at level 4.

| Stage | Objective | Its line |
|---|---|---|
| 1. The Ebb | Topple 4 **brood spires** (explosions do far more; they hatch creatures while you're close) | "The water stepped aside as if it had been asked." |
| 2. The Kelp Cathedral | Light 3 **beacons** (stand by one while it catches; the sea comes harder) | "Someone built these arches. Not us." |
| 3. The Hull Graveyard | Blow open the **wreck-wall** (only blasts move it; gas cans wait in a cache) | "So many ships. Who sailed them?" |
| 4. The Drowned Keep | **Hold the courtyard** for 2 minutes (the clock only runs while someone's inside) | "It has a window that faces the shore." |
| 5. The Heart | Defeat **the Tidemother**, then choose | "She was waiting." |

- **Supply caches** at the start of every stage open the shop (wall and powerplant upgrades aren't offered out here).
- **Regrouping**: finishing an objective sets a checkpoint there and gets the fallen back up.
- **The Tidemother**: 6000 HP, armoured, calls her children from the stage's creatures, and brings her claws down in a
  ring that throws everyone near her back (faster below half health). Her claws and her eye can be broken.
- **Spiral Shards**: one hidden off the path in every stage. They're kept for good, across runs.
- **Endings**: *Strike the Heart* ends the expedition the way the town has always ended them. With all five shards a
  second choice appears: *Lay down the Crest*. What it means is never explained; see `docs/STORY.md`.

The story under the story (why the war never ends) is told only in symbols: the town's Crest, the creatures' Mark,
the Spiral, five petals, tally marks. `docs/STORY.md` is the key, and the rules for adding to it.

## Adding content
For a **new enemy**:
1. Add its model to `Blender/generate_models.py` (`Limb_<Part>` with the pivot at the joint, front facing -Y). Re-run the script.
2. Add an `EnemyDef` to `GameDatabase.FillDefaults()` (and the asset). Choose an `EnemyBehaviour`, or add one plus a method in `Enemy.Behaviour.cs`.
3. Give every limb a `LimbDef`, and give it **its own animation** in `Enemy.PoseRoot` / `AnimateLimbs`: every creature moves in a way you can recognise.
4. Answer the design questions: what does it **ask**, what **counters** it, how does it interact with **wetness**, what's its corpse worth, what does it do **behind a breach**?
5. Add it to a phase's `enemyPool` if needed, and to the bestiary capture. Run the tests and the capture tour.

For a **new tidal card**: add a `TidalCardDef` (data only, if its effect is one of the existing fields). An exclusive card needs a `special` with a behaviour that *only makes sense* in its mode, handled in `TidalCards.cs`.

For a **new stat or mod**: add a `StatDef` (pick a `StatKind`) or a `ModDef` (multipliers/additions on the weapon). New kinds of effect need code in `Player` (`StatBonus` users, `RebuildWeapons`).

For a **new phase**: add a `PhaseDefinition`. If it needs behaviour no field covers, add a field (default off) and handle it in `PhaseManager`, `WaveDirector` or `World`. Never branch on the phase id (R2).
