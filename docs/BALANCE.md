# Balance & Economy

`BALANCE` in `web/js/data.js` holds the live numbers for every formula below. If you change a formula, change it there and here together, then re-run the campaign check (see "How to verify").

## Scaling (n = night number)
| Quantity | Formula | n=1 | n=5 | n=10 | n=20 |
|---|---|---|---|---|---|
| Enemy HP | `1 + 0.16(n−1) + 0.004(n−1)²` | 1.0 | 1.7 | 2.8 | 5.5 |
| Boss HP | `1 + 0.1(n−1)` | — | 1.4 | 1.9 | 2.9 |
| Enemy wall damage | `1 + 0.05(n−1)` | 1.0 | 1.2 | 1.45 | 1.95 |
| Spawn budget | `14 + 9n + 0.9n²` (boss nights ×0.45, plus the boss) | 24 | 82 | 194 | 554 |
| Night length | `min(90, 40 + 2.5n)` s of spawning | 42 | 52 | 65 | 90 |
| Kill bounty | `base × (1 + 0.03(n−1))` | | | | |
| Night bonus | `(60 + 20n) × (0.5 + 0.5·wall%)` | | | | |
| Trap / gadget / crew damage | `base × (1 + 0.08(n−1))` | | | | |

Enemy budget costs: Crabling 1, Drowner 2, Gull 2, Eel 2, Puffer 3, Shellback 5, Siren 8, Clawback 14.

**Why the bounty barely scales:** the number of enemies already grows quadratically. When per-kill bounty also scaled (+7% per night), income snowballed: the test shopper had bought everything by night 12, cruised through the middle nights, and then hit a cliff at night 17. Keep income growth coming from enemy *count*, not per-kill value.

**Boss adds give no bounty and are capped** (`summon.cap`), so bosses can't be farmed and long boss fights can't snowball into a wall breach.

## Starting state
Wall 300 HP, 200 scrap, Service Pistol, 2 weapon slots, 1 gadget slot, 2 crew posts, 3 trap slots.

## Pacing targets (measured)
`DevSim.campaign()` plays 20 nights with a mediocre bot (retargets every 0.5 s, ±12 px aim error, fires 70% of the time) and a fixed shopping list. Target shape, and what the current numbers give:

| Phase | Target | Measured (8 campaigns) |
|---|---|---|
| Nights 1–4 | learn the game, ~0–5% wall loss | 0–3% |
| Night 5 (Crab King) and 6 (eels arrive) | first real check, 10–60% loss, rarely fatal | 0–94%, no falls once the bot triages |
| Nights 7–16 | breathing room with occasional scares | mostly 0, spikes of 15–60% on 11/13/16 |
| Nights 17–19 | tension, 20–80% loss | 0–81% |
| Night 20 (Leviathan) | coin flip for an average player | won 3/8, fell 5/8 |

Humans aim worse than the bot but prioritize better and use combos on purpose. If playtesters find the middle nights boring, raise the HP slope first, not the budget.

## Price bands
| Band | Scrap | Use for |
|---|---|---|
| Trinket | 150–250 | traps, basic mods |
| Core | 300–450 | element mods, crew hires, gadgets |
| Weapon I | 400–700 | first weapons beyond the pistol |
| Weapon II | 900–1400 | specialist weapons |
| Weapon III | 1500–1800 | build-defining weapons |
| Structural | 800–1600 | extra slots |

Upgrades: weapon tier cost is `max(200, price × 0.5) × currentTier`. Crew level cost is `hire × 0.8 × currentLevel`. Wall upgrades scale linearly with level. Repair costs 0.5 scrap per HP.

Selling refunds **50%** of the total spent on that item (R8).

## DPS sanity bands (tier 1, no mods, sustained, including reloads)
The Armory shows this number on every weapon card.
| Weapon | Sustained DPS |
|---|---|
| Pistol | ~29 |
| SMG | ~53 (single target) |
| Scattergun | ~48 (all pellets) |
| Longrifle | ~43 (×pierce) |
| Harpoon | ~31 (pierces everything, knockback) |
| Mortar | ~31 (×targets in AoE) |
| Arc Caster | ~46 (more when wet) |
| Blowtorch | ~55 at short range |

New weapons must land within ±25% of the closest band after you account for their strengths.

**Crew must stay below a held weapon.** A level-1 Rifleman is about 15 DPS and never misses. It once did 60% of all damage at 40+ DPS, which made aiming optional. Passive damage (crew + traps) should stay under ~40% of a night's total in `DevSim` runs.

## How to verify
1. `node web/serve.js` and open `http://localhost:8137/?dev`
2. In the console: `DevSim.campaign()` (full run with economy), `DevSim.report()` (fixed loadouts per night), or `DevSim.night(10, DevSim.PLANS[10], 'storm')`.
3. DevSim stubs out saving, so it never touches a real save.
