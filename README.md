# Tidewall

A browser wall-defense game in the spirit of *The Last Stand* and *Bunny Invasion*. Hold a sea wall for 20 nights against creatures that crawl out of the surf.

Enemies come out of the sea **Wet** and dry off as they cross the sand. Shock and cold rule the waterline and fire rules the wall, so your loadout decides where on the beach you're strongest.

## Play
Open `web/index.html` in a browser. You don't need to install anything.

Or serve it locally: `node web/serve.js`, then go to http://localhost:8137

**Controls:** aim with the mouse and hold left click to fire · **R** reload (tap again in the green zone for a Perfect reload) · **1/2/3, Q, wheel** switch weapon · **Space / E** gadgets · **Esc** pause · **M** mute

## Docs
- [CLAUDE.md](CLAUDE.md): design pillars and rules for working on the game
- [docs/GDD.md](docs/GDD.md): game design document
- [docs/ELEMENTS.md](docs/ELEMENTS.md): status effects and interactions
- [docs/LOADOUTS-AND-CONTENT.md](docs/LOADOUTS-AND-CONTENT.md): loadout rules and content tables
- [docs/BALANCE.md](docs/BALANCE.md): formulas, pacing, and how to run the balance bot (`?dev`)
- [docs/TECH.md](docs/TECH.md): code layout and debug hooks
