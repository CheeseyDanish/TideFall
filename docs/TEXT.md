# TideWall 3D — where the text is

A map of every place the game's words live, for editing them by hand. Paths are from the repo root.

## 1. Content names and descriptions (most of the text)
**`Assets/Content/Data/GameDatabase.asset`** is what the game actually reads. Edit it in Unity (select it in the Project window; the Inspector shows every list), or as text (it's YAML, so search for the words you want to change).

It holds the names and descriptions of:
- enemies (`enemies`)
- guns (`guns`) and melee weapons (`melee`)
- food (`foods`) and condiments (`condiments`)
- shop upgrades (`upgrades`)
- stats (`stats`) and weapon mods (`mods`)
- tidal cards (`cards`)
- defences (`structures`)
- the vendors' shouted lines (`chefLines`, `fishmongerLines`)

The same text is also in **`Assets/Scripts/Core/GameDatabase.cs`**, in `FillDefaults()`. Those are the defaults used to *create* the asset. If you edit the .cs file, run **TideWall → Reset Data To Defaults** so the asset picks the change up. Otherwise just edit the asset; it wins at runtime.

## 2. Phases
**`Assets/Content/Data/PhaseCycle.asset`** (defaults in `Assets/Scripts/Core/PhaseCycle.cs`) holds each phase's `displayName` ("Morning") and `subtitle` (the banner line under "MORNING · DAY 1").

## 3. Menus
| What | File |
|---|---|
| Main menu: New run, Continue, Host/Join, slots, confirmations, typing prompts, the help line | `Assets/Scripts/UI/MainMenu.cs` (`Root()`, `Slots()`, `HostMenu()`, `JoinMenu()`, `Draw()`) |
| Pause menu, settings (every row label and description), controls lists, Stats & Mods, tidal card vote, shop tabs, hotdog stand, Build menu | `Assets/Scripts/UI/Menus.cs` |
| Shop and card layouts' help lines ("D-pad choose · [A] buy…") | `Assets/Scripts/UI/MenuViews.cs` |
| Rebindable action names ("Shoot / throw", "Build defences"…) | `Assets/Scripts/Players/PlayerControls.cs` (`Rebindable`) |
| Graphics option names (anti-aliasing, shadows, display modes) | `Assets/Scripts/Core/Graphics.cs` |

## 4. In-game text (HUD, banners, prompts, toasts)
| What | File |
|---|---|
| HUD: phase line, powerplant/wall line, level, guns-holstered note, vote reminder, game over / RUN OVER, "You're down!", controller disconnected | `Assets/Scripts/UI/PlayerHud.cs` (`Update()`, `CenterMessage()`) |
| What Interact does ("Shop", "Sell 2 corpses (+30)", "Ring the bell", "Pick up…") | each `Prompt()` in `Assets/Scripts/World/`: `ShopKiosk.cs`, `Fishmonger.cs`, `HotdogStand.cs`, `ReadyBell.cs`, `FoodPickup.cs`, `Carryable.cs`, `Corpse.cs`, `GasCan.cs` |
| Player toasts: level up, mods fitted, respec, guns locked, view switched, building, interceptions | `Assets/Scripts/Players/Player.*.cs` (search `Toast(`; mostly `Player.Progression.cs`, `Player.Combat.cs`, `Player.Building.cs`), `Assets/Scripts/World/SandDollarPickup.cs` |
| Banners: joins, IS DOWN, BLED OUT | `Assets/Scripts/Players/Player.Health.cs`, `Assets/Scripts/Players/PlayerManager.cs` (search `Banner(`) |
| Banners: wall breached, core critical, meltdown | `Assets/Scripts/World/Wall.cs`, `Assets/Scripts/World/PowerPlant.cs` |
| Banners: tidal cards (vote, re-roll, card activated, ebbs away, Message in a Bottle) | `Assets/Scripts/Core/TidalCards.cs` |
| Banners: waves, boss rises, gull stole a corpse, structure destroyed | `Assets/Scripts/Enemies/WaveDirector.cs`, `Assets/Scripts/Enemies/Enemy.*.cs`, `Assets/Scripts/World/Structure.cs` |
| Wave preview line ("Incoming: 4 Crabling…", "Night may bring: …") | `Assets/Scripts/Enemies/WaveDirector.cs` (`UpdatePreview`, `Describe`) |
| Reload minigame captions ("RHYTHM · Tap [X] on the beat") | `GameDatabase.asset` → `reloads` (`displayName`, `how`; `{r}` = the reload button), defaults in `GameDatabase.cs` |
| Reticle words (PERFECT!, JAMMED, FUMBLE, SLIP, VENTED!, OPEN), RELOAD hint, combo names (FRENZY, RAMPAGE, UNSTOPPABLE) | `Assets/Scripts/Players/ReloadGame.cs`, `Assets/Scripts/UI/ReticleHud.cs` |
| Status effect names (RAGE, WRONG VIEW…) and ??? sauce lines | `Assets/Scripts/Players/StatusEffects.cs` (`Label`, `Sauce.Table`) |
| Online: host left, version mismatch, game full, connecting | `Assets/Scripts/Net/NetHost.cs`, `Assets/Scripts/Net/NetClient.cs`, `Assets/Scripts/UI/MainMenu.cs` |
| Stall signs over the shop/stands ("SHOP", "HOTDOGS"…) | in the scene: select the stall's `Sign` under World/Town in `Assets/Scenes/TideWall.unity` and edit its TextMeshPro text (the generator's defaults are in `WorldBuilder.cs`) |
| Online menus and messages (join code, password, "Wrong password.", relay errors) | `Assets/Scripts/UI/MainMenu.cs` (`HostMenu`, `JoinMenu`), `Assets/Scripts/Net/NetHost.cs`, `NetClient.cs`, `NetRelay.cs` (`Explain`) |

Tips:
- Text in `<color=#…>` and `<b>` tags is rich text; keep the tags balanced.
- `{btn}` in a prompt is replaced by the player's own button (e.g. `[E]`). Keep it in.
- After editing scripts, run the tests (`docs/TECH.md` → Commands) to make sure nothing broke.
