# Tech Notes

## Running
- **Play:** open `web/index.html` in any modern browser (works from `file://`, R14).
- **Local server (optional):** `node web/serve.js` → http://localhost:8137
- **Dev harness:** add `?dev` to the URL. It loads `js/devsim.js` (see `BALANCE.md`).

## Files
| File | Responsibility |
|---|---|
| `web/js/data.js` | **All content and balance numbers** (R1): `FIELD`, `BALANCE`, `RELOADS`, `WEAPONS`, `MODS`, `GADGETS`, `TRAPS`, `CREW`, `FORTIFY`, `SLOTS`, `RELICS`, `ENEMIES`, `CARDS`, `SYNERGIES` |
| `web/js/audio.js` | `Sfx`: WebAudio-synthesized sounds, throttled per sound name |
| `web/js/game.js` | Run state, night simulation (waves, spawns, pearls), damage pipeline, rendering |
| `web/js/reload.js` | `Reload`: the per-weapon reload minigames (`start / update / press / release / draw`) |
| `web/js/shopscene.js` | `ShopScene`: the daytime castle-window backdrop drawn behind the shop and summary |
| `web/js/ui.js` | `UI`: DOM screens (title, how-to, shop tabs incl. Reliquary, trap placement, tide cards, summaries, pause) |
| `web/js/main.js` | Boot, scaling to the window, input (R down/up), main loop, `window.TW` debug hooks |
| `web/js/devsim.js` | Headless balance bot (dev only) |

Scripts are classic `<script>` tags in dependency order: data → audio → game → reload → shopscene → ui → main. Top-level `const`/`function` declarations are shared globals on purpose (no bundler). Note that `const` globals (`Store`, `UI`…) are **not** properties of `window`.

## State model
- `Game.run`: everything that persists between nights (scrap, **pearls**, **relics**, wall HP, owned items, loadout, placed traps, crew, stats). It is plain JSON, saved to `localStorage['tidewall.run']` whenever the shop opens and deleted on game over. Best night is in `tidewall.best`. Older saves without `pearls`/`relics` are patched when loaded (`UI` → `continue`). Use `hasRelic(id)` to check a relic.
- `Game.N`: one night's simulation (enemies, projectiles, particles, timers). Built by `startNight(cardId)` and thrown away by `finishNight()` / `gameOver()`.
- `Game.state`: `title | shop | place | cards | night | summary | gameover`.

## Night simulation (`updateNight(dt)`)
Order each tick: spawn queue (current wave) → wave timer / rush check → pearls → player → crew → traps → enemies → projectiles → fields/waves → effects → cull dead → end checks. `dt` is in seconds, capped at 50 ms; bosses' deaths and the wall breaking slow time briefly.

### Waves
`buildSpawns()` returns entries `{ t, id, wave }` sorted by wave, then by `t` (seconds after that wave starts). `N.waveInfo[w]` counts each wave's spawns and kills; bosses and summoned adds don't count. When `killed ≥ 50%` of the current wave, `N.nextWaveIn` drops to 3 s. When a wave starts early, any entries still queued from earlier waves spawn at once, so the queue can't stall.

### Reloads
A weapon slot (`N.arms[i]`) carries `reloading`, `rl` (minigame state while reloading), `rt/rTotal` (HUD progress, 0..1) and `perfect` (the Perfect damage bonus, e.g. 0.25, 0 = none). `startReload(a)` → `Reload.start`. Each frame `Reload.update(a, dt, idle)` returns true when done, then `finishReload(a, a.rl.bonus)`. R keydown → `pressReload()` → `Reload.press`, keyup → `releaseReload()`. Per-round styles (`perRound` in `RELOADS`) add ammo one round at a time; when `interrupt` is set, firing with rounds loaded cancels the reload. Holstered weapons with a Deckhand get passive progress only (`idle = true`).

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
