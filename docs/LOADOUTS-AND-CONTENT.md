# Loadouts & Content Rules

## Slot structure
| Slot | Start | Max | Expand cost |
|---|---|---|---|
| Weapon slots | 2 | 3 | Fortify tab |
| Mod sockets per weapon | 1 | 2 | per weapon, Armory |
| Gadget slots | 1 | 2 | Fortify tab |
| Crew posts | 2 | 4 | Fortify tab |
| Trap slots | 3 | 5 | Fortify tab |

**Holstered weapons do not reload** unless you have a **Deckhand** on the crew. This makes "swap instead of reload" a build you choose, not a default.

Mods are owned items. One copy fits one socket. You can move them freely between nights (R8).

## Loadout archetypes the content must support
Every patch must keep **at least one viable build per archetype**:
1. **Stormcaller:** Shock weapons/mods, plus wet sources (Sprinkler, Tide Surge, Storm card), plus Storm Call. Kills at the waterline.
2. **Pyre-keeper:** Fire, Oil Slick, Flare. Kills near the wall. Weak to wet waves and Sirens.
3. **Glacier:** Cold, plus Explosion/Mortar for Shatter, plus Frost Grenade. Crowd control and burst.
4. **Marksman:** Longrifle/Harpoon, Hollow Point, Spotter. Weak points and armored targets. Weak to swarms.
5. **Bulwark:** Plating, Barbs, Mechanic, plus a cheap fast weapon. Tanks hits and lets the wall do the work. Weak to puffers and bosses.
6. **Prospector:** Salvager mods, Scavenger crew, greedy tide cards, pearl hunting (Pearl Diver, Midas Shell). Weaker each night, richer each run.

## Content checklist (fill this for every new item or enemy)
- **Question / Answer:** which enemy or situation does it answer? (For an enemy: which question does it ask?)
- **Weakness:** where does it fail?
- **Synergy:** which element row, item, or crew does it combine with?
- **Tell:** how does the player see it working? (label, visual, sound)
- **Price band:** from `BALANCE.md`

## Enemies (questions they ask)
| Enemy | Question | Good answers | Tell |
|---|---|---|---|
| Crabling | Can you handle many small, fast targets? | Scattergun, SMG, spikes, chains | low red scuttler, eye stalks = weak point |
| Drowner | Baseline: can you hit heads? | anything, weak points | hunched green humanoid, fin-head = weak point |
| Gull | Can you hit flyers that ignore traps? | hitscan, Arc, Rifleman | white bird, pauses before it dives |
| Puffer | Will you kill it **before** it reaches the wall, and **where**? | range, early kills inside packs | yellow spiked ball, swells up near the wall |
| Eel | Can you hit leaping targets? | spread, chains, Arc | dark ribbon, hops; airborne = ignores traps |
| Shellback | Can you get through armor? | pierce, explosions, Frozen, DoT | domed shell, small head in front = weak point |
| Siren | Will you push out to the waterline? (She heals and soaks everyone) | Longrifle, Harpoon, mortar, Storm Call | stands in the surf, singing ring aura |
| Clawback | Can you deal sustained single-target DPS? | Thermal Shock, Marked, Harpoon knockback | big red lobster, raised claw = weak point |
| **Crab King** (5, 15) | Single target vs. adds | split loadout | crown = weak point, summons crablings |
| **Siren Queen** (10) | Long-range pressure while the whole beach is wet | Stormcaller, Marksman | stays in the sea, the whole field soaks every song |
| **Leviathan** (20) | Everything at once | full build | huge, glowing eye = weak point, slams the wall |

## Weapons
| Weapon | Answers | Weakness | Synergy | Reload (GDD) |
|---|---|---|---|---|
| Service Revolver | early game, precise heads | low DPS, 6-round cylinder | Hollow Point, Hair Trigger | Rhythm, interruptible |
| Scattergun | crowds at mid range, eels | falls off at range, armor | Voltaic, Incendiary | Shell by shell, interruptible |
| Rivet SMG | gulls, crablings | armor, ammo hungry | Drum Mag, Cryo stacking | Active |
| Longrifle | Sirens, armor (pierce), heads | slow, bad vs. swarms | Hollow Point, Spotter | Bolt action |
| Harpoon Gun | lines of enemies, knockback | tiny magazine | knocks enemies back into wet sand → Stormcaller | Heavy (fast, harsh) |
| Mortar | packs, armor, Shatter | slow arc, misses movers | Cryo (Shatter), Incendiary (Inferno with oil) | Drop (moving zone) |
| Arc Caster | wet packs, flyers (hitscan) | weak vs. dry single targets | wet sources, Storm Call | Capacitor (mash) |
| Blowtorch | dry enemies at the wall, oil | short range, useless when wet | Oil Slick, Barbs | Pressure (hold/release) |

## Mods
| Mod | Effect | Trade-off |
|---|---|---|
| Incendiary | +Fire | bad on wet enemies |
| Cryo | +Cold | weak damage on its own |
| Voltaic | +Shock | weak vs. dry, isolated targets |
| Piercing | +1 pierce, armor halved | — (uses a socket) |
| Blast Tip | small explosion on hit | smaller weak-point bonus (splash isn't a crit) |
| Hollow Point | weak points ×3 | no gain on body shots |
| Hair Trigger | +35% fire rate | −15% damage, uses ammo faster |
| Drum Mag | +60% magazine | +15% reload time |
| Salvager | +60% scrap from kills | no combat value |

## Gadgets
| Gadget | Effect | Synergy |
|---|---|---|
| Flare | fire field at the cursor | Oil Slick, dry zone |
| Frost Grenade | Cold ×2 in a radius | wet zone (Flash Freeze), Mortar shatter |
| Storm Call | shocks every wet enemy on the field | Stormcaller |
| Depth Charge | delayed heavy explosion | Frozen (Shatter), packs |
| Tide Surge | wave that wets everything and pushes it back | Cold/Shock, saves the wall |
| Overcharge | 5 s of +50% fire rate and no ammo use | any weapon, bosses |

## Traps (ground only, R6)
| Trap | Effect | Synergy |
|---|---|---|
| Spike Strip | light damage over time (6/s) to anything walking over it | crablings, slows from Oil/Chill keep them on it |
| Oil Slick | slows, applies Oiled | Fire → Inferno |
| Tide Mine | one blast per night | packs |
| Shock Post | zaps the nearest enemy | wet sand, Sprinkler |
| Brine Sprinkler | keeps enemies in its area Wet | Shock/Cold near the wall |

## Crew
| Crew | Effect |
|---|---|
| Rifleman | auto-shoots the enemy closest to the wall |
| Mechanic | repairs the wall during the night, plus a free repair after it |
| Spotter | marks the toughest enemy (+35% damage taken) |
| Scavenger | +20% of the night's earnings per level |
| Deckhand | reloads holstered weapons |

## Relics (Reliquary, bought with pearls, R6a)
Relics are one-off and last the whole run. Each one bends a rule rather than adding raw stats, and each leans toward an archetype.
| Relic | ◉ | Effect | Leans toward |
|---|---|---|---|
| Golden Chamber | 3 | Perfect reloads +15% more damage | any, rewards reload skill |
| Siren's Ear | 4 | reload minigame windows 35% wider | players who find the minigames hard |
| Pearl Diver | 2 | pearls drift 40% slower and are bigger | Prospector (more pearls) |
| Coral Mortar | 5 | wall +15% max HP, regrows 1 HP/s at night | Bulwark |
| Brine Heart | 5 | enemies stay Wet twice as long | Stormcaller, Glacier |
| Ember Heart | 5 | burning deals +50% | Pyre-keeper |
| Third Hand | 6 | gadget cooldowns −25% | gadget builds |
| Abyssal Lens | 4 | Sea Fog is far thinner for you | greedy card picks |
| Midas Shell | 6 | +15% scrap from every source | Prospector |

## Tide cards
| Card | Effect | Scrap |
|---|---|---|
| Calm Waters | nothing | ×1.0 |
| Storm | everyone stays Wet | ×1.25 |
| Heatwave | enemies dry 3× faster | ×1.1 |
| Swarm | many more crablings, eels and gulls | ×1.3 |
| Sea Fog | a thick fog bank hides everything past mid-beach; only eyes glint through | ×1.35 |
| Iron Tide | +3 armor to every enemy | ×1.4 |
| Spring Tide | spawns come 30% faster | ×1.25 |
| Lucky Current | 3× pearls | ×1.0 |
| Red Tide | +25% enemy HP and speed | ×1.6 |
