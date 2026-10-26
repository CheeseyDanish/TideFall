# Tidewall — rules for Claude

This repo holds **Tidewall**, an HTML5 wall-defense game. The game lives in `web/`: open `web/index.html` in a browser (no build step), or run `node web/serve.js`. It grew out of a Unity prototype that is still in `C:\Users\Andrew\Wall defence`. That prototype is not part of this repo.

The design documents in `docs/` define the game. These rules are binding for any change you (Claude) make to Tidewall. If a request conflicts with a rule, say which rule and ask before breaking it; if the user confirms, update the doc in the same change so docs and game never disagree.

## The design pillars (in priority order)

1. **Every shot is a decision.** Aim matters (weak points, pearls, lobs), reloads are a skill check (active reload), and *where* on the beach an enemy dies matters (Wet → dry gradient).
2. **Loadouts are answers to questions.** Every enemy asks a question; every item answers at least one and is weak to at least one. No item is strictly better than another item at the same price.
3. **Combos are explicit.** Element interactions follow one small, fixed table (`docs/ELEMENTS.md`). The game always *shows* when a combo fires (label + distinct effect). No hidden multipliers.
4. **Readable chaos.** The screen can get busy, but you can always read wall HP, ammo, reload state, and which enemies are dangerous.
5. **Short loop, long run.** A night lasts 45–90 s. Between nights there is one shop screen and one risk card. A full run is 20 nights (then endless).

## Hard rules

### Content
- **R1 – Data lives in `web/js/data.js`.** All numbers for weapons, mods, gadgets, traps, crew, enemies, cards, and upgrades are defined there. Logic files read data; they never hard-code a balance number that belongs to one item. Shared formulas live in `BALANCE` in `data.js`.
- **R2 – Each new item fills the content checklist** in `docs/LOADOUTS-AND-CONTENT.md` (question it answers, weakness, synergy, tell). Put it in the content tables there when you add it.
- **R3 – No strict upgrades.** A new weapon or mod must lose to an existing one in at least one situation (range, armor, flyers, wet/dry zone, ammo economy, crowd vs. single target).
- **R4 – Element interactions are only the ones in `docs/ELEMENTS.md`.** Adding an element or an interaction means adding a row to that table first. Every interaction needs a floating label and a visual effect.
- **R5 – Enemy tells.** Each enemy must have a silhouette you can tell from the others, a visible weak point if it has one, and a visual cue before any burst action (puffer swelling, siren aura, gull dive pause).
- **R6 – Flying enemies ignore traps; leaping enemies ignore traps while airborne.** Do not break this without updating the docs.

### Economy & balance
- **R7 – Prices follow `docs/BALANCE.md`.** A new item's price comes from the price bands there, not from guesses.
- **R8 – Never punish experimenting.** Mods can be moved between weapons for free between nights. Traps can be picked up and moved for free. Selling refunds 50%.
- **R9 – Risk cards are opt-in.** The player always gets a "Calm Waters" style low-risk option or a choice among three; difficulty modifiers always pay out more scrap.
- **R10 – Difficulty scales through the formulas in `BALANCE`** (HP scale, budget, bounty scale). Do not tune single nights by hand.

### Feel
- **R11 – Every hit gives feedback**: hit flash, a damage number (yellow = weak point, cyan = combo, white = normal), and a sound. Kills give a particle burst. Big events (boss hit, depth charge, wall breach) shake the screen, scaled to how big they are.
- **R12 – The player is never confused about why they lost.** Wall damage flashes the wall, shows the enemy that hit it, and plays a thud. The game-over screen shows the cause (which enemy did the most wall damage).
- **R13 – Respect the player's hands.** Holding fire works for every weapon. All actions have a keyboard binding shown in the HUD. Nothing needs pixel-perfect timing apart from the optional active reload.

### Tech
- **R14 – Runs from `file://`.** Plain classic `<script>` tags, no modules, no bundler, no CDN dependencies. One global namespace per file is fine.
- **R15 – No asset files are required.** Graphics are drawn on the canvas and sound is made with WebAudio. (You may add optional assets later, but the game must still run without them.)
- **R16 – Fixed logical resolution 1280×720**, scaled to fit the window. Game logic uses logical pixels and seconds (`dt`), never frames.
- **R17 – `localStorage` access is wrapped in try/catch.** The game must work with storage blocked.
- **R18 – Verify in a browser.** After any gameplay change, load the page, play at least one night (or use the debug hooks in `docs/TECH.md`), and check the console for errors before you call it done.
- **R19 – Balance changes are measured, not guessed.** Before and after changing numbers in `BALANCE`, `ENEMIES`, `WEAPONS` or `CREW`, run `DevSim.campaign()` (open `?dev`) a few times and compare with the pacing table in `docs/BALANCE.md`. Report the before/after.
- **R20 – Passive damage stays a supporting act.** Crew + traps should do under ~40% of a night's damage in DevSim runs. Aiming must always matter.
- **R21 – Never touch the player's save from tooling.** Test code stubs `Store.set` / `Store.del` (DevSim already does).

## Where things are
| Doc | What it decides |
|---|---|
| `docs/GDD.md` | Vision, core loop, controls, screens |
| `docs/ELEMENTS.md` | Status effects and the interaction table (binding) |
| `docs/LOADOUTS-AND-CONTENT.md` | Slot rules, content checklist, current content tables |
| `docs/BALANCE.md` | Formulas, price bands, pacing targets |
| `docs/TECH.md` | Code layout, entity model, debug hooks |
