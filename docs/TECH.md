# Tech Notes

## Running
- **Play:** open `web/index.html` in any modern browser (works from `file://`, R14).
- **Local server (optional):** `node web/serve.js` → http://localhost:8137
- **Dev harness:** add `?dev` to the URL. It loads `js/devsim.js` (see `BALANCE.md`).

## Files
| File | Responsibility |
|---|---|
| `web/js/data.js` | **All content and balance numbers** (R1): `FIELD`, `BALANCE`, `WEAPONS`, `MODS`, `GADGETS`, `TRAPS`, `CREW`, `FORTIFY`, `SLOTS`, `ENEMIES`, `CARDS`, `SYNERGIES` |
| `web/js/audio.js` | `Sfx`: WebAudio-synthesized sounds, throttled per sound name |
| `web/js/game.js` | Run state, night simulation, damage pipeline, rendering |
| `web/js/ui.js` | `UI`: DOM screens (title, how-to, shop tabs, trap placement, tide cards, summaries, pause) |
| `web/js/main.js` | Boot, scaling to the window, input, main loop, `window.TW` debug hooks |
| `web/js/devsim.js` | Headless balance bot (dev only) |

Scripts are classic `<script>` tags in dependency order: data → audio → game → ui → main. Top-level `const`/`function` declarations are shared globals on purpose (no bundler).

## State model
- `Game.run`: everything that persists between nights (scrap, wall HP, owned items, loadout, placed traps, crew, stats). It is plain JSON, saved to `localStorage['tidewall.run']` whenever the shop opens and deleted on game over. Best night is in `tidewall.best`.
- `Game.N`: one night's simulation (enemies, projectiles, particles, timers). Built by `startNight(cardId)` and thrown away by `finishNight()` / `gameOver()`.
- `Game.state`: `title | shop | place | cards | night | summary | gameover`.

## Night simulation (`updateNight(dt)`)
Order each tick: spawn queue → pearls → player → crew → traps → enemies → projectiles → fields/waves → effects → cull dead → end checks. `dt` is in seconds, capped at 50 ms; bosses' deaths and the wall breaking slow time briefly.

### Damage pipeline (the only way to hurt enemies)
```
hit(e, base, {elements, head, headMult, armorPierce, ignoreArmor, perfect, knock, chain, src})
  → weak point × → perfect × → Conductor / Thermal Shock → Marked × → armor
  → dealDamage(e, dmg)   (numbers, flash, kill)
  → applyFire / applyCold / Shock stun + chain()   (ELEMENTS.md table)
dot(e, amount)   burning / spikes. Ignores armor, numbers batched every 0.5 s
explode(x, y, r, dmg, o)   ignores armor, falls off to 50% at the edge, carries elements
```
If you add a new damage source, go through `hit` / `dot` / `explode` so elements, labels and feedback stay consistent (R4, R11).

### Enemies
Every enemy has a hitbox: `e.x` is the center, `e.y` the feet (bottom), and `e.lift` raises it (eel leaps). `boxOf(e)` gives the box. `d.head` is the weak-point sub-box as fractions, measured from the **left/front** edge. Behavior branches on data flags: `flying`, `leaps`, `siren`, `heavy`, `boss`, `summon`. Each look is a painter in `DRAW[id]`.

### Adding content (checklist)
1. Add the entry to `data.js` (and to the `docs/LOADOUTS-AND-CONTENT.md` tables, R2).
2. If it needs a new behavior, branch on a **data flag**, not the id, where you can.
3. Enemy: add a `DRAW` painter with a distinct silhouette and a visible weak point (R5), a `PACK` size, and a `SPAWN_WEIGHT`.
4. Shop: armory/gadget/trap/crew lists render from data automatically.
5. Synergy hints: add to `SYNERGIES` and set the flag in `UI.loadoutKeys` if it is a new kind of interaction.
6. Run `DevSim.campaign()` a few times and compare against the pacing table in `BALANCE.md`.

## Debug hooks (`window.TW` in the console)
| Call | Effect |
|---|---|
| `TW.give(5000)` | add scrap |
| `TW.night(n)` | set the next night number (in the shop) |
| `TW.killAll()` | kill everything on screen |
| `TW.skip()` | end the current night |
| `TW.god()` | huge wall |
| `TW.Game` | live state |
