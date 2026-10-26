// Tidewall — content & balance data. Single source of truth for numbers (CLAUDE.md R1).
'use strict';

const W = 1280, H = 720;
const FIELD = {
  groundY: 610,
  wallLeft: 40,
  wallFace: 190,
  wallTop: 430,
  shoreX: 1080,
  gunner: { x: 168, y: 418 },          // shoulder pivot
  crewX: [128, 98, 68, 152],
  trapMinX: 250,
  trapMaxX: 1050,
};

const BALANCE = {
  // Tuned with DevSim.campaign() — see docs/BALANCE.md before changing.
  hpScale: n => 1 + 0.16 * (n - 1) + 0.004 * (n - 1) * (n - 1),
  bossHpScale: n => 1 + 0.1 * (n - 1),
  dmgScale: n => 1 + 0.05 * (n - 1),
  bountyScale: n => 1 + 0.03 * (n - 1),
  effectScale: n => 1 + 0.08 * (n - 1),     // traps, gadgets, crew
  budget: n => Math.round(14 + 9 * n + 0.9 * n * n),
  bossBudgetMult: 0.45,
  duration: n => Math.min(90, 40 + 2.5 * n),
  nightBonus: n => 60 + 20 * n,
  startScrap: 200,
  wallBaseHp: 300,
  wallHpPerLevel: 100,
  repairCostPerHp: 0.5,
  sellRefund: 0.5,
  tierMax: 5,
  tierCost: (price, tier) => Math.round(Math.max(200, price * 0.5) * tier),
  tierDmg: t => 1 + 0.2 * (t - 1),
  tierMag: t => 1 + 0.1 * (t - 1),
  tierReload: t => 1 - 0.06 * (t - 1),
  socketCost: 450,
  crewLevelCost: (hire, lvl) => Math.round(hire * 0.8 * lvl),
  crewMaxLevel: 3,
  dryTime: 3.5,
  headMult: 2,
  perfectBonus: 0.25,
  perfectZone: [0.42, 0.6],
  jamPenalty: 0.6,
  swapTime: 0.22,
  pearlEvery: [14, 24],
  pearlValue: n => 25 + 6 * n,
  victoryNight: 20,
};

// ---- Weapons -----------------------------------------------------------
// kind: bullet | lob | flame | arc
const WEAPONS = {
  pistol: {
    name: 'Service Pistol', price: 0, kind: 'bullet',
    dmg: 13, rate: 3, mag: 10, reload: 1.1, spread: 0.012, pellets: 1, speed: 2000, pierce: 0,
    color: '#ffe9a8', len: 22, body: '#6b7280',
    desc: 'Reliable sidearm. Precise enough for weak points.',
  },
  scattergun: {
    name: 'Scattergun', price: 450, kind: 'bullet',
    dmg: 7, rate: 1.4, mag: 6, reload: 1.9, spread: 0.16, pellets: 7, speed: 1700, pierce: 0, falloff: 520,
    color: '#ffd27a', len: 30, body: '#8b5a2b',
    desc: 'Seven pellets. Shreds crowds and leaping eels; weak past mid-beach.',
  },
  smg: {
    name: 'Rivet SMG', price: 600, kind: 'bullet',
    dmg: 7, rate: 11, mag: 40, reload: 1.6, spread: 0.05, pellets: 1, speed: 2100, pierce: 0,
    color: '#fff3c4', len: 26, body: '#374151',
    desc: 'Bullet hose. Great on gulls and crabs, poor into armor.',
  },
  longrifle: {
    name: 'Longrifle', price: 950, kind: 'bullet',
    dmg: 65, rate: 0.9, mag: 5, reload: 2.0, spread: 0.0, pellets: 1, speed: 3200, pierce: 3, armorPierce: true,
    color: '#ffffff', len: 40, body: '#4b3621',
    desc: 'Pierces 3 and halves armor. Reaches Sirens in the surf.',
  },
  harpoon: {
    name: 'Harpoon Gun', price: 1100, kind: 'bullet',
    dmg: 45, rate: 1.1, mag: 3, reload: 1.6, spread: 0.0, pellets: 1, speed: 1400, pierce: 99, armorPierce: true, knock: 70, thick: 4,
    color: '#c7d2fe', len: 36, body: '#1f2937',
    desc: 'Skewers everything in a line and shoves it back toward the sea.',
  },
  mortar: {
    name: 'Mortar Tube', price: 1300, kind: 'lob',
    dmg: 50, rate: 1.0, mag: 4, reload: 2.4, spread: 0, pellets: 1, splash: 75,
    color: '#fca5a5', len: 28, body: '#3f3f46',
    desc: 'Lobs shells at the cursor. Ignores armor; cold + blast = shatter.',
  },
  arc: {
    name: 'Arc Caster', price: 1600, kind: 'arc', innate: ['shock'],
    dmg: 16, rate: 4, mag: 20, reload: 2.0, spread: 0, pellets: 1, range: 760,
    color: '#7dd3fc', len: 30, body: '#1e3a5f',
    desc: 'Hitscan lightning, innate Shock. Devastating on wet packs.',
  },
  blowtorch: {
    name: 'Blowtorch', price: 1300, kind: 'flame', innate: ['fire'],
    dmg: 4, rate: 22, mag: 80, reload: 2.2, spread: 0.12, pellets: 1, speed: 560, life: 0.5,
    color: '#fb923c', len: 28, body: '#7c2d12',
    desc: 'Short-range fire stream, innate Fire. Melts dry enemies at the wall.',
  },
};

// ---- Mods --------------------------------------------------------------
const MODS = {
  incendiary: { name: 'Incendiary', price: 300, tag: 'fire', desc: 'Hits apply Fire.', apply: s => s.elements.add('fire') },
  cryo:       { name: 'Cryo Rounds', price: 300, tag: 'cold', desc: 'Hits apply Cold (Chill).', apply: s => s.elements.add('cold') },
  voltaic:    { name: 'Voltaic', price: 350, tag: 'shock', desc: 'Hits apply Shock (chains).', apply: s => s.elements.add('shock') },
  piercing:   { name: 'Piercing', price: 250, desc: '+1 pierce, halves armor.', apply: s => { s.pierce += 1; s.armorPierce = true; } },
  blast:      { name: 'Blast Tip', price: 400, desc: 'Small explosion on hit (50% dmg, 40px).', apply: s => { s.blast = Math.max(s.blast || 0, 40); } },
  hollow:     { name: 'Hollow Point', price: 300, desc: 'Weak point hits x3 instead of x2.', apply: s => { s.headMult = 3; } },
  hair:       { name: 'Hair Trigger', price: 250, desc: '+35% fire rate, -15% damage.', apply: s => { s.rate *= 1.35; s.dmg *= 0.85; } },
  drum:       { name: 'Drum Mag', price: 200, desc: '+60% magazine, +15% reload time.', apply: s => { s.mag = Math.round(s.mag * 1.6); s.reload *= 1.15; } },
  salvager:   { name: 'Salvager', price: 300, desc: '+60% scrap from kills with this weapon.', apply: s => { s.salvage = (s.salvage || 1) + 0.6; } },
};

// ---- Gadgets -----------------------------------------------------------
const GADGETS = {
  flare:   { name: 'Flare', price: 350, cd: 12, desc: 'Burning field at cursor for 5s. Dries, then ignites.' },
  frost:   { name: 'Frost Grenade', price: 350, cd: 14, desc: 'Cold x2 in a large radius. Wet targets Flash Freeze.' },
  storm:   { name: 'Storm Call', price: 450, cd: 18, desc: 'Lightning strikes every Wet enemy on the field.' },
  depth:   { name: 'Depth Charge', price: 400, cd: 18, desc: 'Heavy delayed blast at cursor. Ignores armor.' },
  surge:   { name: 'Tide Surge', price: 400, cd: 20, desc: 'A wave soaks everything and shoves it back.' },
  overcharge: { name: 'Overcharge', price: 450, cd: 25, desc: '5s: +50% fire rate, no ammo used.' },
};

// ---- Traps -------------------------------------------------------------
const TRAPS = {
  spikes:    { name: 'Spike Strip', price: 150, w: 70, desc: 'Damages anything walking across it.', dps: 12 },
  oil:       { name: 'Oil Slick', price: 200, w: 90, desc: 'Slows 35% and Oils. Fire on Oiled = Inferno.' },
  mine:      { name: 'Tide Mine', price: 250, w: 26, desc: 'Big blast once per night.', dmg: 90, radius: 85 },
  post:      { name: 'Shock Post', price: 350, w: 20, desc: 'Zaps the nearest enemy in range.', dmg: 14, every: 1.2, range: 130 },
  sprinkler: { name: 'Brine Sprinkler', price: 250, w: 110, desc: 'Keeps enemies in its spray Wet.' },
};

// ---- Crew --------------------------------------------------------------
const CREW = {
  rifleman:  { name: 'Rifleman', price: 500, color: '#a3e635', desc: 'Shoots the enemy closest to the wall.', dmg: 11, every: 0.8, perLevel: 0.5 },
  mechanic:  { name: 'Mechanic', price: 400, color: '#fbbf24', desc: 'Repairs the wall during the night (+10%/lvl after).', repair: 2, after: 0.1 },
  spotter:   { name: 'Spotter', price: 550, color: '#f87171', desc: 'Marks the toughest enemy: +35% damage taken.', every: 7, mark: 6, bonus: 0.35 },
  scavenger: { name: 'Scavenger', price: 350, color: '#c084fc', desc: '+20% of the night\'s earnings per level.', cut: 0.2 },
  deckhand:  { name: 'Deckhand', price: 450, color: '#38bdf8', desc: 'Reloads your holstered weapons (half speed, faster per level).', slow: 2.0, perLevel: 0.4 },
};

// ---- Fortify -----------------------------------------------------------
const FORTIFY = {
  reinforce: { name: 'Reinforce', max: 10, cost: l => 150 * l, desc: '+100 max wall HP.' },
  plating:   { name: 'Plating', max: 5, cost: l => 300 * l, desc: '-1 damage from every enemy hit.' },
  barbs:     { name: 'Barbs', max: 5, cost: l => 250 * l, desc: 'Enemies hitting the wall take 5 dmg/lvl.' },
};
const SLOTS = {
  weapon: { name: 'Weapon Slot', start: 2, max: 3, cost: [1500] },
  gadget: { name: 'Gadget Slot', start: 1, max: 2, cost: [1200] },
  crew:   { name: 'Crew Post', start: 2, max: 4, cost: [800, 1600] },
  trap:   { name: 'Trap Slot', start: 3, max: 5, cost: [400, 800] },
};

// ---- Enemies -----------------------------------------------------------
// head: weak-point box as fractions of the body box [x0, y0, x1, y1] (x0=left/front)
const ENEMIES = {
  crab:      { name: 'Crabling', hp: 18, speed: 75, dmg: 3, atk: 1.0, bounty: 4, w: 34, h: 22, cost: 1, from: 1, head: [0.2, 0, 0.8, 0.35], color: '#f97316' },
  drowner:   { name: 'Drowner', hp: 40, speed: 38, dmg: 6, atk: 1.0, bounty: 7, w: 30, h: 62, cost: 2, from: 1, head: [0.1, 0, 0.9, 0.27], color: '#6b8f71' },
  gull:      { name: 'Gull', hp: 14, speed: 130, dmg: 4, atk: 0, bounty: 6, w: 38, h: 20, cost: 2, from: 2, head: [0, 0.1, 0.3, 0.7], color: '#e5e7eb', flying: true },
  puffer:    { name: 'Puffer', hp: 28, speed: 50, dmg: 40, atk: 0, bounty: 9, w: 34, h: 34, cost: 3, from: 3, head: null, color: '#facc15' },
  eel:       { name: 'Eel', hp: 24, speed: 115, dmg: 4, atk: 1.4, bounty: 7, w: 54, h: 16, cost: 2, from: 6, head: [0, 0, 0.25, 1], color: '#334155', leaps: true },
  shellback: { name: 'Shellback', hp: 110, speed: 24, dmg: 12, atk: 0.8, bounty: 18, w: 66, h: 44, cost: 5, from: 4, head: [0, 0.4, 0.22, 0.85], color: '#5b6b3a', armor: 4 },
  siren:     { name: 'Siren', hp: 70, speed: 35, dmg: 8, atk: 0.8, bounty: 30, w: 30, h: 70, cost: 8, from: 7, head: [0.1, 0, 0.9, 0.25], color: '#5eead4', siren: true },
  clawback:  { name: 'Clawback', hp: 380, speed: 20, dmg: 28, atk: 0.6, bounty: 55, w: 96, h: 70, cost: 14, from: 9, head: [0, 0.05, 0.3, 0.45], color: '#dc2626', armor: 3, heavy: true },
  // bosses
  crabking:  { name: 'Crab King', hp: 1000, speed: 14, dmg: 22, atk: 0.7, bounty: 400, w: 150, h: 96, head: [0.35, 0, 0.65, 0.22], color: '#ea580c', armor: 2, heavy: true, boss: 'crabking',
               summon: { id: 'crab', n: 2, every: 8, cap: 5 } },
  sirenqueen:{ name: 'Siren Queen', hp: 2600, speed: 0, dmg: 0, atk: 0, bounty: 600, w: 60, h: 120, head: [0.15, 0, 0.85, 0.2], color: '#2dd4bf', heavy: true, boss: 'sirenqueen',
               summon: { id: 'drowner', n: 2, every: 7, cap: 8, enraged: { n: 3, every: 5 } }, song: { every: 5, enragedEvery: 3.5, heal: 0.06 } },
  leviathan: { name: 'Leviathan', hp: 6500, speed: 10, dmg: 90, atk: 0.5, bounty: 800, w: 230, h: 190, head: [0, 0.1, 0.22, 0.4], color: '#1e3a8a', armor: 8, heavy: true, boss: 'leviathan',
               summon: { id: 'eel', n: 4, every: 8, cap: 10 } },
};
const BOSS_NIGHTS = { 5: 'crabking', 10: 'sirenqueen', 15: 'crabking', 20: 'leviathan' };
const PACK = { crab: [3, 5], drowner: [1, 3], gull: [2, 3], puffer: [1, 2], eel: [2, 3], shellback: [1, 2], siren: [1, 1], clawback: [1, 1] };

// ---- Tide cards --------------------------------------------------------
const CARDS = {
  calm:    { name: 'Calm Waters', mult: 1.0, desc: 'An ordinary night.', icon: '☾' },
  storm:   { name: 'Storm', mult: 1.25, desc: 'Rain all night: every enemy stays Wet.', icon: '⛈', wetAlways: true },
  heat:    { name: 'Heatwave', mult: 1.1, desc: 'Enemies dry 3x faster.', icon: '☀', dryMult: 3 },
  swarm:   { name: 'Swarm', mult: 1.3, desc: 'Far more crablings, eels and gulls.', icon: '✦', swarm: true },
  fog:     { name: 'Sea Fog', mult: 1.35, desc: 'The far beach is hidden in fog.', icon: '≋', fog: true },
  iron:    { name: 'Iron Tide', mult: 1.4, desc: 'All enemies gain +3 armor.', icon: '⛨', armor: 3 },
  spring:  { name: 'Spring Tide', mult: 1.25, desc: 'Spawns arrive 30% faster.', icon: '⇶', fast: 0.7 },
  lucky:   { name: 'Lucky Current', mult: 1.0, desc: 'Three times as many pearls.', icon: '◉', pearls: 3 },
  red:     { name: 'Red Tide', mult: 1.6, desc: 'Enemies have +25% HP and speed.', icon: '☠', hp: 1.25, spd: 1.25 },
};

// ---- Synergy hints (shown in shop; informational only) -----------------
const SYNERGIES = [
  { id: 'conductor', name: 'Conductor', desc: 'Shock + a Wet source', test: k => k.shock && (k.wetSource) },
  { id: 'thermal', name: 'Thermal Shock', desc: 'Cold + Fire', test: k => k.cold && k.fire },
  { id: 'inferno', name: 'Inferno', desc: 'Fire + Oil Slick', test: k => k.fire && k.oil },
  { id: 'shatter', name: 'Shatterer', desc: 'Cold + explosions', test: k => k.cold && k.explosive },
  { id: 'flash', name: 'Flash Freeze', desc: 'Cold + a Wet source', test: k => k.cold && k.wetSource },
  { id: 'tidecaller', name: 'Undertow', desc: 'Knockback toward the surf + Shock', test: k => k.knock && k.shock },
  { id: 'juggler', name: 'Juggler', desc: 'Deckhand + 2+ weapons', test: k => k.deckhand && k.weapons >= 2 },
  { id: 'prospector', name: 'Prospector', desc: 'Salvager or Scavenger', test: k => k.greed },
  { id: 'marksman', name: 'Marksman', desc: 'Hollow Point + Spotter', test: k => k.hollow && k.spotter },
];
