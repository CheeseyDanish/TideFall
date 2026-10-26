# TideWall 3D

A co-op wall-defense **roguelike** for **1–4 players, in split screen, online, or both**. Play it as a **first-person shooter**, or press one button for the original **top-down** view. Behind a breachable sea wall stands the town's **Nuclear Sand Powerplant**; when it melts down, the run is over.
- **Morning:** sell corpses for **sand dollars**, upgrade, spend level-up points, eat hotdogs (??? sauce at your own risk). The run saves.
- **Evening and Night:** vote on a **tidal card**, then sharks, saltwater crocs, sea urchins, octopuses and more **ride the breaking waves** onto a beach twice the size of the original.

Shoot their limbs off, swing an anchor, throw a gas can, and keep the core cool.

Built with **Unity 6.5 (URP)**. Every model was generated in **Blender** from a script. It's the 3D follow-up to [Tidewall 2D](https://github.com/CheeseyDanish/Website) (the browser game on my website).

## Highlights
- **Roguelike runs.** A meltdown ends the run (and deletes its save); a Legacy record keeps your best. Runs are suspendable each morning.
- **Tidal cards.** Vote on a modifier at the start of every fight (Red Tide, King Tide, Gold Rush, Glass Cannon…). Cards last several waves and stack. Some exist **only online** (Buddy Beam, Message in a Bottle) or **only in split screen** (Screen Swap, Shared Snacks).
- **Levels, stats and weapon mods.** XP from kills, stat points to spend (Vigor, Brawn, Aim, Swiftness, Quick Hands, Fortune) in the Stats menu, respec for sand dollars, and a choice of weapon mods every 5 levels.
- **Sand dollars** are the money: they fly to whoever made the kill, and teammates can intercept them. **Golden pearls** come from the rare giant clam.
- **Build defences** anywhere on the beach: sandbag barricades, urchin spikes, kelp snares, bait buckets, harpoon turrets and pearl cannons.
- **A shop like the 2D one**: tabs of item cards with pictures. Vendors at the hotdog stand and fish cart. You can keep walking with any menu open.
- **Reload minigames from the 2D game** (rhythm, shells, bolt, heavy, drop, mash, pressure, zone), drawn around your reticle; Perfect reloads hit harder. **Kill combos** pay bonus sand dollars and XP.
- **Melee first**: the first 3 waves are melee only; every weapon swings its own way (chop, slash, sweep, thrust, slam). Guns kick hard.
- **The Nuclear Sand Powerplant** behind a wall of 20 breachable sections. Creatures go for breaches and the core.
- **Waves you can see**: creatures surf in on breakers that swell the sea and break on the shore. Every creature moves its own way (crabs scuttle sideways, puffers puff up into spiky balls…).
- **First person or top-down, per player.** Your gun and melee weapon in view, a crosshair, mouse or stick look. Switch views any time; split screen can mix them.
- **Split-screen drop-in co-op.** Press any button on a controller (Xbox, PlayStation, Switch Pro, generic gamepads, HID joysticks) or any key to join, up to 4 players.
- **Online multiplayer.** Host from the main menu; friends on your network find you in a list, others join by address. Each machine can have several players too.
- **Saves.** Three slots, autosave every morning, save from the pause menu, "try again" from your last morning.
- **Settings.** Volume (master, effects, interface, ambience), field of view, sensitivity, invert look, aim assist, screen shake, fullscreen, graphics quality, and full **control rebinding** for keyboard & mouse and controllers.
- **A title screen with a view:** looking out of the keep's window at the morning beach (beach volleyball, umbrellas, gulls, circling shark fins).
- **Phases you can edit.** Morning, Evening and Night are just entries in a data asset. Add, remove or reorder them without code.
- **14 sea creatures, 7 of them new to the 3D version:** sharks that beach themselves, death-rolling saltwater crocs, spiny sea urchins, ink-squirting octopuses, stinging jellyfish, hermit crabs and Clawback lobsters. The Glizzy Monster boss arrives every 5th night.
- **Limb damage.** Shoot a crab's claws off and it barely hurts. Take a gull's wing and it crawls. Cut a croc's tail and it can't swipe. Intact corpses sell for more.
- **Wet beach.** Creatures come out of the sea dripping; more drops = wetter. Wet things take extra shock damage and don't burn.
- **Nautical melee:** beach spade, cutlass, oar, boat hook (shock) and anchor.
- **The hotdog stand** (on tires): ketchup = rage, mustard = speed, ??? sauce = anything. That includes an upside-down screen, the wrong view, giant mode, and a 0.01% jackpot.
- **Gas cans that *may* explode.**

## Play
1. Install **Unity 6000.5.10f1** (via Unity Hub).
2. Open this folder as a project. On first open, run **TideWall → Setup Project**; it only needs to run once.
3. Press Play, or build with **TideWall → Build Windows**.
4. Pick **New game** in the main menu (or Continue, Load, Host or Join). Others press any button or key to join.

| | Gamepad | Keyboard + mouse |
|---|---|---|
| Move / look | left / right stick | WASD / mouse |
| Shoot (throw when carrying) | RT | left click |
| Melee | RB | right click / F |
| Interact | A | E |
| Dash | LB | Space |
| Reload / swap gun | X / Y | R / Q |
| Switch view (first person / top-down) | R3 | V |
| Stats & mods / tidal card vote | Select | Tab / C |
| Build defences | D-pad down | B |
| Menus (choose) | D-pad | Arrow keys / wheel |
| Pause menu (settings, save) | Start | Esc |

Everything except the sticks and mouse look can be rebound in **Settings → Controls**.

### Playing online
- **Host:** Main menu → Host online game. Your pause menu shows your address.
- **Join:** Main menu → Join online game. Games on your network are listed; otherwise type the host's address.
- Over the internet the host needs TCP port 7777 forwarded (changeable in the Host menu). Windows asks to let TideWall through the firewall the first time you host.

## For developers
- **Design:** [docs/GDD.md](docs/GDD.md)
- **Architecture and commands:** [docs/TECH.md](docs/TECH.md), covering setup, tests, builds, the screenshot tour, the online check, the protocol and the Blender pipeline
- **Rules for AI-assisted changes:** [CLAUDE.md](CLAUDE.md)
- **Where the game's text is (to edit it):** [docs/TEXT.md](docs/TEXT.md)

```bash
unity test . --mode PlayMode --output Logs/TestResults.xml
```

## Status
Split screen, online play (LAN and by address), saves, settings and the roguelike systems are in. Online play is host-authoritative with no relay server or matchmaking: internet games need the host's port forwarded. Balance (card numbers, XP curve, mod strength, the doubled beach's pacing) hasn't been playtested yet.
