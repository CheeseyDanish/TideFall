# Elements & Status Effects (binding)

There are three damage elements, **Fire**, **Cold**, and **Shock**, plus two environmental states, **Wet** and **Oiled**. Explosions are a delivery type, not an element.

This table is the *entire* interaction system. Code must not add interactions that aren't listed here (CLAUDE.md R4).

## Statuses
| Status | Source | Effect | Ends |
|---|---|---|---|
| **Wet** | Coming out of the sea; Storm card; Siren songs; Brine Sprinkler trap; Tide Surge gadget | Modifies elements (see below) | 3.5 s after leaving the water (×card dry multiplier). Fire removes it at once. |
| **Oiled** | Oil Slick trap | Slowed 35% while standing in the oil | 5 s after leaving the oil, or consumed by Fire |
| **Burning** | Fire hit on a dry, non-oiled target | Damage over time (ignores armor) | 3 s (refreshed by new fire hits) |
| **Chilled** | Cold hit | −40% speed, stacks up to 3 | 2.5 s. At 3 stacks → Frozen. |
| **Frozen** | 3 Chill stacks, or Cold on a Wet target | Can't move or attack. Armor is ignored. | 1.5 s (2 s if it came from Wet) |
| **Shocked** | Shock hit | 0.2 s stun, chains (below) | instant |
| **Marked** | Spotter crew | Takes +35% damage | 6 s |

## Interaction table
| Hit ↓ / Target state → | Normal | **Wet** | **Oiled** | **Burning** | **Frozen** |
|---|---|---|---|---|---|
| **Fire** | Burning | **Steam**: Wet removed, small damage, no burn | **Inferno**: burn ×3 dps for 4 s, spreads to oiled enemies within 60 px | refresh burn | **Thermal Shock**: big bonus damage, thaws |
| **Cold** | +1 Chill | **Flash Freeze**: Frozen 2 s at once, Wet removed | +1 Chill | **Douse**: puts out the burn, +1 Chill | refresh Frozen |
| **Shock** | chain to 1 target (60%) | **Conductor**: hit ×1.5, chains to 3 targets within 180 px | chain to 1 | chain to 1 | chain to 1, **ignores armor** |
| **Explosion** | ignores armor | ignores armor | if the explosion has Fire, Inferno | — | **Shatter** (see below) |

**Shatter:** an enemy killed while Frozen breaks into shards that deal damage in a 60 px radius and give +1 Chill to everyone they hit.

**Chain hits never chain again.** A chain hit uses the target's state (a wet chain target takes ×1.5). **An enemy can start a chain at most once every 0.25 s**, so multi-pellet and very fast weapons can't multiply chains.

**Freeze limits:** after a Freeze ends, the enemy can't be frozen again for 2 s (5 s for heavy enemies). It can still be Chilled. Heavy enemies (Clawback, bosses) are only frozen for half as long and stunned by Shock for only 0.05 s.

## Reading guide for the player
- Near the **sea**, enemies are wet. **Shock** and **Cold** are strong there, and **Fire** is weak.
- Near the **wall**, enemies are dry. **Fire** is strong there.
- **Sprinklers, the Storm card, Tide Surge, and Sirens** move the wet zone. So the Siren is both a threat and an opportunity.
- **Oil + Fire** is the strongest burst, but it needs set-up.
- **Cold → Fire** (Thermal Shock) is the combo for single targets. **Cold + Explosion** (Shatter) is the combo for crowds.

## Labels (R11)
| Combo | Floating label | Color |
|---|---|---|
| Steam | `STEAM` | grey-white |
| Inferno | `INFERNO!` | orange |
| Thermal Shock | `THERMAL SHOCK!` | magenta |
| Flash Freeze | `FLASH FREEZE` | ice blue |
| Douse | `DOUSED` | light blue |
| Conductor | `CONDUCTOR` | electric cyan |
| Shatter | `SHATTER` | white-blue |
