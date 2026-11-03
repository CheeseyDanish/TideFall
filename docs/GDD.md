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

## Look
Low-poly with flat colours, given form by bevelled edges, baked shading (dark where parts meet and toward the ground, a slight tone per face), faint surface grain, and ambient light that's cooler from the sky with a warm bounce off the ground.

## Views
- **First person** (the default). The camera is at your eyes; you see your gun bottom-right (and your melee weapon sweeping across when you swing), and a small crosshair in your colour. Shots go to whatever is under the crosshair. Mouse or right stick looks around; the left stick / WASD moves relative to where you look.
- **Third person**. The camera sits over your right shoulder and looks where you look, with the same mouse / right-stick look and centre crosshair as first person; you see your sailor, their gun (and its attachments) and what's around you. Movement is relative to where you look. The camera pulls in when a wall or the ground gets between it and your sailor. (It replaced the original top-down view.)
- Switch any time with **View** (R3, V) or from the pause menu. Each player picks their own view. The default view for new players is a setting.
- In first person your own body is hidden from your camera; in either view your own name tag is too, and name tags float over teammates. Controllers get gentle aim assist in both views (it can be turned off).

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
Every creature has a job. The **Field guide** (pause menu) lists each one's job and answer.

| Enemy | Its job | What only it does | Answer |
|---|---|---|---|
| **Crabling** | The swarm | With 3+ others close it's faster and hits harder; 1.5x damage to the wall; scuttles in zig-zag bursts | Scattergun, oar sweep |
| **Drowner** | The climber | After a few seconds at the wall, more than half of them **climb over it**, no breach needed | Heads; take its legs and it can't climb |
| **Gull** (from wave 5) | The raider | Flies over the wall at the powerplant; steals corpses in the morning | Shoot the wings |
| **Hermit Crab** | The turtler | Armour 4; below 60% HP it **pulls into its shell** for 2 s and takes 30% damage | Knock the shell off, then it's fast but soft |
| **Puffer** | The bomb | Explodes on you or the wall, 3x damage to the wall | Pop it far off, or inside a crowd |
| **Sea Urchin** | The mine | Bristles up and **fires a ring of spines** every few seconds; hurts anyone who hits it by hand | Keep your distance and shoot it |
| **Jellyfish** | The snare | Stings everyone close, leaves you **slowed**, worse in water | Stay on dry sand; cut tentacles |
| **Shark** | The sea's guard | Hunts swimmers; beached, it flops at the wall, 2x damage | Stay out of the sea; take the tail |
| **Eel** | The hunter | Aggro 14 m: ignores the wall to chase sailors, **lunges from 7 m** and the lunge itself bites | Spread and sidestep; the tail stops the lunges |
| **Shellback** | The escort and ram | **Shields creatures within 4.5 m** (they take 60%; a blue line shows it); 2.5x damage to the wall; shoves | Kill it first; only the head is soft |
| **Saltwater Croc** | The ambusher | Grabs and death-rolls a sailor; tail-swipes | Teammates break the grab; cut the tail |
| **Octopus** | The controller | Grabs from 3.5 m and pulls; inks your screen | Cut tentacles |
| **Clawback** (lobster) | The bruiser | **Claws block 65% from the front** while both are on; charges; hits shove you far | Flank it, or take the claws first |
| **Giant Clam** (rare) | The treasure | Armour 10 while shut; drops **golden pearls** | Hit it when it gapes open |
| **Glizzy Monster** (boss) | The boss | Huge, summons crabs, shakes the screen | Everyone, everything |

### Losing limbs
A limb coming off rarely ends it. Lost claws and arms halve its damage, legs slow it, and a stump keeps pumping for a few seconds. **Without one leg it hops and lists; without both it drags itself along on its arms** (and a Drowner can't climb). Even losing its **head or eyes** kills only 60% of the time (`headlessChance`): otherwise it **stumbles about blind** and bleeds out in 4 to 8 s, and whoever took its head gets the kill. A blow big enough to kill it outright still does. **Explosions blow limbs off** (`blastLimbChance`), never the head.

Behind a breach, creatures go for the powerplant and anyone in town. Bosses arrive on nights where `day % bossEveryDays == 0`.

## The Last Light (main menu)

You're stranded on a sandbar. Help is **10 days** out, and the lighthouse is your only hope: keep it lit so they can find you. Last the tenth night and you're **rescued**; lose the tower, or let the lamp stay out for 25 s at night, and you're **lost at sea**. Either way a full-screen tally ends the run. Lamp oil you bring home buys the Keeper's Log's permanent upgrades.

**Day and night.** Day (60 s the first time, then 45 s) is for getting ready: the shop's open, the lamp's off and burning nothing, and nothing attacks. Night brings the creatures and the rising sea, and every night the sea starts higher. The fifth and tenth nights have a boss. After the fifth the sea pulls back for a low tide, in daylight: run out and loot the wrecks. A **calendar** on the ground-floor wall crosses off the nights, and the top of the HUD counts the days until rescue.

**The tower.** A door on the south side leads to the ground floor, with the **shop** (guns, attachments, melee, upgrades, defences) and the calendar. A ramp winds up inside the wall to the wider lamp room, and a gap in its sill opens onto a railed **catwalk**. Nothing can reach you indoors. On the catwalk and at the lamp only flyers can, but the creatures are still chewing on the tower while you're up there.

**The lamp** sweeps the water at night, drying and burning what it touches, and burns oil. Take it (**Interact** in the lamp room) to aim it yourself: stronger, and thirstier. Oil comes from fishing, salvage and rendering corpses.

**The flare.** Spend 20 oil (the lever in the lamp room, or **Dash** while you're at the lamp) and the whole lamp goes off at once for 3 s: blindingly bright, and everything within 50 m is stunned for 4 s (bosses shake it off in a quarter of that). It recharges in 10 s.

**The dark.** Past where you can wade, the sea is full of rocks and the wrecks of ships that hit them. By night you see them only when the beam comes round. With the lamp out, the world nearly vanishes and red eyes look back from the water.

**The try-pot.** Interact with it and every corpse on the island flies in, day or night, for sand dollars and oil. It does this by itself at the end of each night.

**Sharks** are tougher, faster and notice you from further off. They run down swimmers, and they'll throw themselves up the sand at anyone within 9 m of the water.

## Tide Caller (The Last Light, 2 to 4 players)

One player sends the sea against everyone else, on The Last Light's sandbar. **The Last Light → Tide Caller** in split screen, or **Host Tide Caller** online. The first tide waits until someone takes **the conch** from its rock by the tower (it needs a second player to fight).

**The sailors** play The Last Light as usual, without the Keeper's Log's upgrades. They win if the light lasts **10 tides**; the fifth and tenth are bosses, and the last boss has to die.

**The Tide Caller** has no body and can't be hurt. They win if the lighthouse falls, the lamp stays dark too long, or every sailor is down at once. They see the whole sandbar in an **isometric view** and move a marker over the water; **Melee** swings the view round a quarter turn, and **View** switches to a closer view that the mouse or right stick can turn. Rings show each sailor and the circle no creature can be called inside:

- **Call (Fire; hold to keep calling).** Spends brine: a creature rises at the marker. It has to be water at least knee deep, at least 12 m from any sailor, and not in slack water. Brine keeps filling through slack water, faster each tide, up to a cap that also grows. The creatures on offer widen with the tide. Next creature: **Swap**, the d-pad, arrows or wheel.
- **Rally (Aim; 2 brine, 8 s).** Everything within 18 m makes for the marker, faster than usual, for 7 s. It still bites whoever it meets. Not flyers, clams or bosses.
- **Take over (Interact).** Drive the nearest creature within 9 m from just behind it: 30% faster and 50% harder-hitting than on its own. **Fire** strikes (the usual wind-up, so it can be dodged), **Dash** lunges, **Interact** lets go. It takes the same damage it always would; when it dies you're back above, with 3 s before the next one. Not flyers or clams.

**Each side's best moment pays.** Downing a sailor gives the Caller 6 brine. The driven creature wears a "TIDE CALLER" sign anyone can see: killing it drops a 60 sand dollar bounty and locks the Caller out of taking over for 8 s. The sailors' status line shows how much brine the Caller is holding.

A boss rises in the direction the Caller is looking. Until the first tide starts, **Back** puts the conch down. If the Caller leaves, the usual waves take over and the conch goes back on its rock for anyone to take. A match doesn't count as a Last Light run: no lamp oil, no records.

## Players
- **The sailor is animated**: legs stride with your speed, the gun arm aims where you look, the other arm swings the melee weapon in its style, both arms go up to carry, the head follows your gaze, you lean into a dash and crawl when down.
- **Health 100** (+ Sea Legs, + Vigor). At 0 you're **down**: you crawl and bleed out over 30 s. A teammate holding Interact next to you for 2 s revives you at 40%. Bleed out and you're back at the start of the next phase.
- **Dash** (short burst). Carrying weight and wading slow you.
- **Carry**: pick up corpses and gas cans, stacked up to weight 3. In first person you pick up **what you're looking at** (within 4.5 m); otherwise the nearest thing. **Fire throws** the top item. A corpse thrown into the fishmonger's cart sells itself. Corpses lie on the sand (never sunk into it).
- **Grabbed** (croc/octopus): mash any button to break free, or get a teammate to hit the grabber.

### Gunplay
Every shot kicks: the view snaps up and settles (heavier guns kick harder), the gun jolts back, a muzzle flash and an ejected shell, the crosshair spreads and recovers. Hits flash the creature, make it flinch, and pop a **damage number** (yellow with "!" on weak points); kills flash a red X with a thunk.

### The wall under attack
Each of the 20 sections has its own HP, and a creature hits the one in front of it. Damage shows on the stone in stages: **cracks** (below 70%), **holes and a broken merlon** (below 40%), then **gaping holes with a heap at its foot** (below 15%), with a burst of debris at each stage, and at 0 it collapses. The HUD has a **strip with one cell per section** under the powerplant bar: green to red, flashing white as it's hit, dark when it's down.

### Menus with a mouse
Every menu works three ways at once: **hover** chooses, **click** picks and **right-click** goes back (including shop tabs, tidal cards and casino tables); arrows or the d-pad and Interact do the same. While a menu is open the mouse points rather than turns your view, and WASD and the left stick still walk (R6a). The click that closes a menu doesn't fire your gun.

### Fishing
Patches of bubbles appear on Part I's shore while the shop's open, and at the waterline in The Last Light. **Interact** casts; wait for the bite and strike with **Interact** (too soon or too late and it's gone). Then **hold Fire to raise the green zone, let go to sink**, and keep it on the fish until the meter fills. Back or walking off gives up. Nine catches, in `GameDatabase.fish`: money, lamp oil, healing, a pearl, a boot, a soda can, and one that isn't a fish.

### Inventory (Inventory button: I / d-pad up; also in the pause menu)
You own any number of guns but **carry three** (`gunSlots`). Buying or taking a fourth swaps it with the gun in your hands; that one goes to your **stash**, and you can bring it back free from the Inventory. **Swap** cycles the three, and each slot has its own button (1, 2, 3 / d-pad left, d-pad right, left stick press). Swapping takes a moment (`swapSeconds`): the new gun comes up from below the view. The HUD lists the three, with the one in your hands picked out. The Inventory shows what you carry, your stash, each gun's five slots and its skin, and your melee weapons' skins. Saved with the run.

### Gun attachments (shop → Attachments, for the gun in your hands; or the Inventory, for any gun)
Every gun has **five slots: scope, barrel, laser sight, stock and ammo**, one part in each, each with its own model on the gun. A part is **bought once, per sailor, for the run**: after that it fits any of your guns free, and comes off free, from the Inventory. A new part replaces what's in that slot.

| Part | Slot | Price | Effect |
|---|---|---|---|
| Brass Sight | scope | 120 | 1.3x zoom, 45% less spread |
| Sea-Glass Reflex | scope | 170 | 1.5x zoom, 30% less spread, +5% fire rate |
| Spyglass Scope | scope | 260 | 2.2x zoom, +60% range, +10% damage, 60% less spread, 10% slower fire |
| Barnacle Brake | barrel | 140 | half the kick, 15% less spread |
| Coral Choke | barrel | 190 | 45% less spread, +10% range, hits shove harder |
| Long Barrel | barrel | 220 | +20% damage, +30% range, +15% kick |
| Harbour Beam | laser | 150 | red beam, 30% less spread |
| Glowfish Beam | laser | 190 | green beam, 15% less spread, 20% less kick |
| Angler Lure | laser | 240 | blue beam, 10% less spread, +30% damage to limbs |
| Driftwood Stock | stock | 130 | 40% less kick, 20% less spread |
| Sharkskin Grip | stock | 150 | +15% fire rate, 15% less kick |
| Whalebone Stock | stock | 170 | reloads 20% faster, 25% less kick |
| Incendiary Rounds | ammo | 200 | fire: sets dry creatures alight (wet ones steam); 5% less damage |
| Eel-Shock Rounds | ammo | 220 | shock: hits wet creatures hard |
| Harpoon-Tip Rounds | ammo | 180 | every shot pierces one more creature, +5% damage |
| Blasting Caps | ammo | 280 | 1.6 m blast on impact; 15% less damage, 10% slower fire |
| Hollow Points | ammo | 160 | +50% damage to limbs |
| Drum Magazine | ammo | 180 | +60% magazine (comes loaded), 15% slower reload |
| Quick-Draw Clip | ammo | 160 | reloads 35% faster |

A laser draws a beam to the point under your crosshair and a dot where it lands, and everyone can see it.

### Aiming down the sights (hold: right mouse / left trigger)
The gun comes up to the middle of the view, the view zooms (`aimZoom`, or the fitted scope's `zoom`), the look slows to match, shots tighten (`aimSpread`) and the kick is smaller. Not with a menu open, arms full or mid-reload. In third person the camera moves in over the shoulder.

### Skins
A skin changes how a weapon looks and nothing else, per weapon, from the Inventory. It draws a pattern over the model with no texture (`TideWall/Skin`), and its preview in the Inventory is the weapon wearing it. Online, everyone sees them. Unlocks **outlive runs** (the Legacy record): some are free, the rest come with **all-time kills**, or sooner for **golden pearls**.

| Skin | Pattern | Kills, or pearls |
|---|---|---|
| Factory, Rusted Hull, Dune Camo | none, camo, camo | free |
| Tiger Shark | stripes | 150, or 2 |
| Coral Reef | spots | 300, or 2 |
| Dragonscale | scales | 500, or 3 |
| Ketchup & Mustard | waves | 750, or 3 |
| Nuclear Sand | hazard stripes | 1000, or 4 |
| Gilded Galleon | solid gold | 1500, or 5 |
| Mother of Pearl | shifts colour with the angle | 2000, or 6 |
| Deep Trench | glowing veins that pulse | 3000, or 8 |
| Lava Vent | glowing cracks that pulse | 4000, or 10 |

### How a fight feels
- **Shots:** a spring kick that overshoots and settles, the barrel wandering slightly, sparks out of the muzzle, a kick of sand where a shot lands. Each gun has its own sound; firing empty clicks.
- **Hits:** the creature rocks away from the hit and swells for an instant, and a spray leaves the wound both ways. Damage numbers pop. A shell glancing a shot throws sparks and clinks, so you can tell you're hitting armour. A weak point gets a bigger hit marker and a bright ting.
- **Melee:** a landed swing hangs for a moment (`meleeHitStop`, double for the anchor), the creature stalls, the view kicks, and a streak follows the blade through the strike.
- **Wind-ups (`EnemyDef.windup`):** a creature attacking a player stops, rears back and flashes red, then throws itself forward. The blow lands only if you're still in reach, so **a dash dodges it**. A crab gives you a fifth of a second; a Clawback, more than half. (Attacks on the wall and the powerplant don't wait.)
- **Stagger (`EnemyDef.poise`):** take enough of its HP in quick succession and it reels, and its wind-up is cancelled. Losing a limb does the same.
- **Deaths:** it's thrown along the killing blow, tumbles over and thumps onto the sand, then becomes the corpse.

### How creatures move
Every creature speeds up and turns at a rate that suits its size, leans into its turns, kicks up sand on land, and differs a little from others of its kind. When chasing, a pack spreads wide of you and closes from the sides instead of queueing. Each kind has its own rhythm: crabs scuttle in zig-zag bursts, hermits dart and stop, drowners lurch, eels slither in wide S curves, jellyfish surge on each pulse, octopuses jet, sharks sweep, and a Clawback charges once it's seen you. All 13 regular creatures have new models with jointed legs, flippers, jaws or rippling segments (`Part_*`).

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
- **The shop** works like the 2D game's: tabs (**Guns**, **Attachments**, **Melee**, **Upgrades**, **Defences**; Swap/Reload to switch) of **item cards** with a **picture of the item**, its stats, its price, and whether you own or have it equipped.
- **Vendors** stand behind the stalls: the **hotdog chef** (white coat, tall hat, moustache) and the **fishmonger** (yellow oilskins, sou'wester). They turn to whoever comes near, wave, and shout lines ("Ketchup for rage, mustard for speed!").
- **Menus never stop you walking**: the d-pad / arrow keys (and mouse wheel) drive menus while the left stick / WASD still walks and you can still look around. A stall's menu closes if you walk away from it.

## The Lucky Barnacle (casino)
A booth in town (striped awning, a roulette wheel on top) run by a crab in a top hat, with a **Tide Machine** slot cabinet on either side (walk up to one to play slots straight away). Its front is a row of **game cards with pictures**; each table is green baize in a brass rail, and a win makes the rail flare gold and sand dollars rain across the felt. On Part II's seabed the crab's card crate sits opposite the supply cache. Every game is played with the **team's sand dollars** (no real money), the bet going from 5 to 1,000 SD. In Part I the tables only deal **between fights**; Part II never stops, so neither does the crab. Every game has a **How to play** page on the table. **Left/right changes the bet from any row**, and **Reload repeats the last wager in one press** (spin again, deal again, re-bet the same roulette chips and spin, draw in video poker, skip ahead in Fathoms). After each round the main action stays selected, so Interact does the same.

| Game | How it works | Pays |
|---|---|---|
| **Slots** (the Tide Machine) | Three reel windows of pictures (shells, starfish, hotdogs, anchors, golden pearls, the Glizzy Monster) scroll and stop left to right with a bounce; pull the lever with Reload. When the first two match, the third reel **holds its breath** (spins longer, its window glowing). **High Tide:** each losing spin raises a tide meter beside the reels; at 10 the next spin is **free and pays double**. About 92% back over time, High Tide included. | Glizzy ×3 = 80 · Pearl 25 · Anchor 16 · Hotdog 10 · Starfish 8 · Shell 8 · any two shells 4 |
| **Blackjack** | Six-deck shoe. Hit, stand, double on any two cards, split a pair once (aces get one card each). Dealer stands on all 17s and checks for blackjack. No insurance. | 1 to 1; blackjack 3 to 2 |
| **Roulette** | European wheel (one zero). Chips on single numbers, red/black, odd/even, 1–18/19–36 and the dozens, as many as you like, then spin. | number 35 to 1 · dozen 2 to 1 · the rest 1 to 1 |
| **Video Poker** | Jacks or Better, 9/6. Hold any of five cards, draw once. | royal flush 800 · straight flush 50 · four of a kind 25 · full house 9 · flush 6 · straight 4 · three of a kind 3 · two pair 2 · jacks or better 1 |
| **Fathoms** | Baccarat as sailors play it. Bet on the **Sailor**'s hand, the **Captain**'s, or **Slack Water** (a tie). Cards count A = 1, 2–9, tens and faces 0, and only the last digit counts ("fathoms"); deeper (closer to 9) wins. **High Tide** (8 or 9 on two cards) stops the draw; otherwise the real baccarat drawing rules apply. The dealer shows each card a second apart and says which rule it followed. | Sailor 1 to 1 · Captain 1 to 1 less a 5% harbour fee · Slack Water 8 to 1 |

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
- **Bad:** Upside-down camera, reversed controls, Tiny, Fisheye, Slow, **wrong view** (swaps first ↔ third person), ink, tunnel vision.
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
| Look | right stick | mouse |
| Shoot / throw | RT | left click |
| Melee | RB | F |
| Interact (shop, sell, eat, revive, pick up) | A / Cross | E / Enter |
| Back (close menu) | B / Circle | Backspace |
| Reload | X / Square | R |
| Swap gun (shop: next tab) | Y / Triangle | Q / middle click |
| Dash | LB | Space / Shift |
| Switch view | R3 | V |
| **Stats & mods / tidal card vote** | Select / Back | Tab / C |
| **Inventory** | D-pad up | I |
| **Fishing** | Interact casts and strikes, trigger reels | E casts and strikes, left mouse reels |
| **Aim down sights** (hold) | Left trigger | Right mouse |
| **Gun slots 1, 2, 3** | D-pad left, d-pad right, left stick press | 1, 2, 3 |
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
**Start / Esc**: Resume · Switch view · **Inventory** · **Stats & mods** · Settings · Save game · **Debug tools** · Quit to main menu · Quit to desktop. Offline, the game pauses while anyone has it open; online the beach keeps going.

| Settings | |
|---|---|
| **Audio** | Master, Effects, Interface, Ambience (surf) |
| **Game** | Start in first person · Field of view · Mouse/stick sensitivity · Invert look · Aim assist · Screen shake · Damage numbers · Show frame rate · Debug tools |
| **Graphics** | Display mode (borderless / exclusive fullscreen / windowed) · Resolution (native by default) · VSync · Frame rate limit · Render scale (50–200%) · Anti-aliasing (Off, FXAA, SMAA, MSAA 2/4/8x; MSAA 4x default) · Shadows (off/low/medium/high) · Bloom · Post-processing · Draw distance · Particle density · Quality preset |
| **Controls** | Keyboard & mouse and Controller lists: pick an action, press the new key or button. Reset all controls. |

## Debug menu (playtesting)
**F1** (or **Debug tools** in the pause menu), offline or when hosting; Settings → Debug tools turns it off. It doesn't pause the game.
- **Switches:** god mode (the whole team), infinite ammo, noclip (fly where you look; Dash flies fast), freeze creatures, collider outlines (creatures red, weak points yellow, players green, defences blue, stalls purple), a stats overlay (fps, time scale, phase or stage, creatures, purse, casino result), and time scale (frozen, ¼, ½, 1, 2, 4×).
- **Players & money:** +100 / 1,000 / 10,000 sand dollars, empty the purse, +5 pearls, level up (you, or everyone ×5), every weapon, refill ammo, heal, revive, clear status effects, teleport to your aim point, and a test player on a virtual controller (for split screen).
- **Creatures:** spawn any creature (the Tidemother too) where you aim, 1/3/5/10 at a time; kill them all.
- **Waves & days** (Part I): next phase, go to any day, clear this wave, a tidal card vote now. **Wall & powerplant:** repair or break them.
- **Part II:** go to any stage (from Part I too), finish the stage's objectives, refill the tide clock, play either ending at the Heart, find every Spiral Shard.
- **Tidal cards:** any card on or off. **Casino:** open it anywhere; keep it open during fights.
- **Legacy file:** edit runs, records, shards and the peace; nothing is written to `legacy.json` until you pick Write.

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
