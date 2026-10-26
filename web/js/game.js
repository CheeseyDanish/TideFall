// Tidewall — run state, night simulation, rendering.
'use strict';

// ---- helpers ------------------------------------------------------------
const rand = (a, b) => a + Math.random() * (b - a);
const randi = (a, b) => Math.floor(rand(a, b + 1));
const clamp = (v, a, b) => v < a ? a : v > b ? b : v;
const pick = arr => arr[Math.floor(Math.random() * arr.length)];
const EMPTY = new Set();
const _rgbCache = {};
function hexRgb(h) {
  if (_rgbCache[h]) return _rgbCache[h];
  let v = h.slice(1);
  if (v.length === 3) v = v.split('').map(c => c + c).join('');
  return (_rgbCache[h] = [parseInt(v.slice(0, 2), 16), parseInt(v.slice(2, 4), 16), parseInt(v.slice(4, 6), 16)]);
}
const rgb = (c, k = 1, a = 1) => `rgba(${clamp(c[0] * k, 0, 255) | 0},${clamp(c[1] * k, 0, 255) | 0},${clamp(c[2] * k, 0, 255) | 0},${a})`;
function mixInto(c, hex, t) { const B = hexRgb(hex); for (let i = 0; i < 3; i++) c[i] += (B[i] - c[i]) * t; }

const Store = {
  get(k) { try { const v = localStorage.getItem(k); return v ? JSON.parse(v) : null; } catch (e) { return null; } },
  set(k, v) { try { localStorage.setItem(k, JSON.stringify(v)); } catch (e) { /* storage blocked */ } },
  del(k) { try { localStorage.removeItem(k); } catch (e) { /* storage blocked */ } },
};

// ---- run (persistent between nights) --------------------------------------
const Game = {
  state: 'title', run: null, N: null, paused: false, time: 0,
  input: { mx: 700, my: 400, down: false, tap: 0 },
  placeSel: null,
};

function newRun() {
  return {
    night: 1, scrap: BALANCE.startScrap,
    wallHp: BALANCE.wallBaseHp,
    fort: { reinforce: 0, plating: 0, barbs: 0 },
    slots: { weapon: SLOTS.weapon.start, gadget: SLOTS.gadget.start, crew: SLOTS.crew.start, trap: SLOTS.trap.start },
    weapons: { pistol: { tier: 1, sockets: 1, mods: [null], spent: 0 } },
    mods: {}, gadgets: {}, traps: {}, placed: [], crew: [],
    loadout: { weapons: ['pistol', null, null], gadgets: [null, null] },
    stats: { kills: 0, heads: 0, perfects: 0, pearls: 0, earned: 0, wallDmgBy: {} },
    endless: false,
  };
}
const wallMax = run => BALANCE.wallBaseHp + run.fort.reinforce * BALANCE.wallHpPerLevel;
const crewLvl = (run, id) => run.crew.filter(c => c.id === id).reduce((a, c) => a + c.lvl, 0);
const modsUsed = (run, id) => Object.values(run.weapons).reduce((a, w) => a + w.mods.filter(m => m === id).length, 0);
const modsFree = (run, id) => (run.mods[id] || 0) - modsUsed(run, id);
const trapsPlaced = (run, id) => run.placed.filter(p => p.id === id).length;

function weaponStats(run, id) {
  const def = WEAPONS[id], own = run.weapons[id];
  const s = {
    id, def, kind: def.kind,
    dmg: def.dmg * BALANCE.tierDmg(own.tier), rate: def.rate,
    mag: Math.round(def.mag * BALANCE.tierMag(own.tier)), reload: def.reload * BALANCE.tierReload(own.tier),
    spread: def.spread, pellets: def.pellets, speed: def.speed, pierce: def.pierce, armorPierce: !!def.armorPierce,
    knock: def.knock || 0, splash: def.splash || 0, range: def.range || 0, life: def.life || 0, falloff: def.falloff || 0,
    elements: new Set(def.innate || []), headMult: BALANCE.headMult, blast: 0, salvage: 1,
  };
  own.mods.forEach(m => m && MODS[m].apply(s));
  return s;
}

function saveRun() { if (Game.run) Store.set('tidewall.run', Game.run); }
function loadRun() { return Store.get('tidewall.run'); }
function bestNight() { return Store.get('tidewall.best') || 0; }

// ---- spawn planning ---------------------------------------------------------
const SPAWN_WEIGHT = { crab: 3, drowner: 4, gull: 2, puffer: 1.6, eel: 2, shellback: 1.5, siren: 0.6, clawback: 0.7 };

function bossFor(n) {
  if (BOSS_NIGHTS[n]) return BOSS_NIGHTS[n];
  if (n > BALANCE.victoryNight && n % 5 === 0) return pick(['crabking', 'sirenqueen', 'leviathan']);
  return null;
}

function buildSpawns(n, card) {
  let budget = BALANCE.budget(n);
  const boss = bossFor(n);
  if (boss) budget *= BALANCE.bossBudgetMult;
  const dur = BALANCE.duration(n) * (card.fast || 1);
  const pool = Object.keys(PACK).filter(id => ENEMIES[id].from <= n);
  const weight = id => SPAWN_WEIGHT[id] * (card.swarm && (id === 'crab' || id === 'eel' || id === 'gull') ? 3 : 1);
  const total = pool.reduce((a, id) => a + weight(id), 0);
  const sirenCap = n >= 7 ? 1 + Math.floor((n - 7) / 4) : 0;
  let sirens = 0;
  const list = [];
  let guard = 0;
  while (budget >= 1 && guard++ < 500) {
    let r = Math.random() * total, id = pool[0];
    for (const p of pool) { r -= weight(p); if (r <= 0) { id = p; break; } }
    const cost = ENEMIES[id].cost;
    if (cost > budget) continue;
    if (id === 'siren' && sirens >= sirenCap) continue;
    let size = randi(PACK[id][0], PACK[id][1]);
    if (card.swarm && (id === 'crab' || id === 'eel')) size += 1;
    // most packs spread over the night, some bunch into surges
    const sr = Math.random();
    const t = sr < 0.15 ? dur * rand(0.55, 0.62) : sr < 0.27 ? dur * rand(0.86, 0.94) : Math.pow(Math.random(), 0.9) * dur;
    for (let k = 0; k < size && budget >= cost; k++) {
      list.push({ t: t + k * 0.4, id });
      budget -= cost;
      if (id === 'siren') sirens++;
    }
  }
  list.sort((a, b) => a.t - b.t);
  const shift = list.length ? list[0].t - 1.5 : 0;
  list.forEach(s => { s.t = Math.max(1.5, s.t - shift); });
  if (boss) list.push({ t: 4, id: boss });
  list.sort((a, b) => a.t - b.t);
  return list;
}

// ---- night setup -------------------------------------------------------------
function startNight(cardId) {
  const run = Game.run, n = run.night, card = CARDS[cardId];
  const arms = run.loadout.weapons.slice(0, run.slots.weapon).filter(Boolean).map(id => {
    const s = weaponStats(run, id);
    return { id, s, ammo: s.mag, reloading: false, rt: 0, rTotal: 0, tried: false, perfect: false, jam: false, cd: 0, recoil: 0 };
  });
  Game.N = {
    t: 0, n, card, cardId, eff: BALANCE.effectScale(n),
    queue: buildSpawns(n, card), qi: 0,
    enemies: [], bullets: [], shells: [], flames: [], fields: [], parts: [], texts: [], bolts: [], pearls: [], waves: [],
    traps: run.placed.map(p => ({ id: p.id, x: p.x, armed: true, cd: rand(0, 1), anim: 0 })),
    crew: run.crew.map((c, i) => ({ id: c.id, lvl: c.lvl, x: FIELD.crewX[i], cd: rand(0.3, 1.2), aim: 0, flash: 0 })),
    arms, cur: 0, prev: arms.length > 1 ? 1 : 0, swapT: 0, aim: 0,
    gadgets: run.loadout.gadgets.slice(0, run.slots.gadget).map(id => id ? { id, cd: 0 } : null),
    overcharge: 0,
    earned: { bounty: 0, pearls: 0 }, kills: 0, heads: 0, perfects: 0, pearlsHit: 0,
    shake: 0, wallFlash: 0, wallHitBy: null,
    pearlT: rand(...BALANCE.pearlEvery) / (card.pearls || 1),
    over: false, overT: 0, endT: 0, boss: null, slow: 0, muzzle: 0, startHp: run.wallHp,
    bannerT: 3,
  };
  Game.state = 'night';
  Game.paused = false;
  Sfx.play('horn');
}

// ---- enemies -----------------------------------------------------------------
function spawnEnemy(id, x, opts = {}) {
  const N = Game.N, d = ENEMIES[id], card = N.card;
  const hp = d.hp * (d.boss ? BALANCE.bossHpScale(N.n) : BALANCE.hpScale(N.n)) * (card.hp || 1);
  const e = {
    id, d, x, y: FIELD.groundY, hp, maxHp: hp,
    speed: d.speed * (card.spd || 1) * rand(0.9, 1.1), armor: (d.armor || 0) + (card.armor || 0),
    w: d.w, h: d.h, lift: 0,
    wet: !d.flying, wetT: BALANCE.dryTime, oiled: 0, inOil: false,
    burn: 0, burnDps: 0, chill: 0, chillT: 0, frozen: 0, thawImm: 0, stun: 0, mark: 0, flash: 0,
    atkT: 0.3, attacking: false, kbV: 0, anim: Math.random() * 10, emerge: opts.noEmerge ? 0 : 0.8,
    labelT: -9, dotAcc: 0, dotT: 0, dead: false, state: 'walk', timer: rand(0, 1), timer2: rand(2, 4),
    leapCd: rand(0.6, 1.6), leapT: -1,
  };
  if (d.flying) {
    e.alt = rand(200, 350); e.y = e.alt; e.x = W + 40; e.emerge = 0; e.state = 'fly';
  }
  if (d.siren) e.holdX = FIELD.shoreX + rand(30, 110);
  if (d.boss === 'sirenqueen') { e.x = 1185; e.holdX = 1185; e.emerge = 2; }
  if (d.boss === 'leviathan') { e.emerge = 3; }
  if (d.boss) { N.boss = e; N.bannerT = 3; N.bannerBoss = d.name; Sfx.play('horn'); }
  if (e.emerge) e.y = FIELD.groundY + e.h * 0.9;
  N.enemies.push(e);
  return e;
}

const boxOf = e => { const b = e.y - e.lift; return { l: e.x - e.w / 2, t: b - e.h, r: e.x + e.w / 2, b }; };
function inBox(e, x, y) { const b = boxOf(e); return x >= b.l && x <= b.r && y >= b.t && y <= b.b; }
function isHead(e, x, y) {
  const hd = e.d.head; if (!hd) return false;
  const b = boxOf(e), w = b.r - b.l, h = b.b - b.t;
  return x >= b.l + hd[0] * w && x <= b.l + hd[2] * w && y >= b.t + hd[1] * h && y <= b.t + hd[3] * h;
}
function distToBox(e, x, y) {
  const b = boxOf(e);
  const dx = Math.max(b.l - x, 0, x - b.r), dy = Math.max(b.t - y, 0, y - b.b);
  return Math.hypot(dx, dy);
}
const centerOf = e => { const b = boxOf(e); return { x: (b.l + b.r) / 2, y: (b.t + b.b) / 2 }; };
const grounded = e => !e.d.flying && e.lift < 4 && e.emerge <= 0.2;

// ---- feedback ----------------------------------------------------------------
function text(x, y, str, color = '#fff', size = 14, life = 0.8) {
  const N = Game.N; if (!N || N.texts.length > 140) return;
  N.texts.push({ x: x + rand(-6, 6), y, str, color, size, life, max: life, vy: -50 });
}
function label(e, str, color) {
  const N = Game.N;
  if (N.t - e.labelT < 0.5) return;
  N.labelAt = N.labelAt || {};
  if (N.t - (N.labelAt[str] || -9) < 0.35) return;   // one label per combo type at a time keeps packs readable
  N.labelAt[str] = N.t;
  e.labelT = N.t;
  const c = centerOf(e);
  N.texts.push({ x: c.x, y: c.y - e.h * 0.6 - 10, str, color, size: 17, life: 1.1, max: 1.1, vy: -35, bold: true });
  Sfx.play('combo');
}
function burst(x, y, color, count = 10, speed = 160, size = 3, life = 0.6, grav = 300, kind = 'dot') {
  const N = Game.N; if (!N) return;
  for (let i = 0; i < count && N.parts.length < 700; i++) {
    const a = Math.random() * Math.PI * 2, s = speed * rand(0.3, 1);
    N.parts.push({ x, y, vx: Math.cos(a) * s, vy: Math.sin(a) * s - speed * 0.3, life: life * rand(0.6, 1), max: life, color, size: size * rand(0.6, 1.3), grav, kind });
  }
}
function shake(a) { if (Game.N) Game.N.shake = Math.min(18, Game.N.shake + a); }
function bolt(x1, y1, x2, y2, color = '#a5f3fc', width = 2, life = 0.12) {
  const pts = [[x1, y1]], segs = Math.max(3, Math.floor(Math.hypot(x2 - x1, y2 - y1) / 26));
  for (let i = 1; i < segs; i++) {
    const t = i / segs;
    pts.push([x1 + (x2 - x1) * t + rand(-9, 9), y1 + (y2 - y1) * t + rand(-9, 9)]);
  }
  pts.push([x2, y2]);
  Game.N.bolts.push({ pts, color, width, life, max: life });
}

// ---- damage pipeline (rules: docs/ELEMENTS.md) ---------------------------------
function hit(e, base, o = {}) {
  if (e.dead) return;
  const N = Game.N, el = o.elements || EMPTY;
  let dmg = base, combo = false;
  let ignoreArmor = !!o.ignoreArmor || e.frozen > 0;
  if (o.head) dmg *= o.headMult || BALANCE.headMult;
  if (o.perfect) dmg *= 1 + BALANCE.perfectBonus;
  if (el.has('shock') && e.wet) { dmg *= 1.5; combo = true; if (!o.chain) label(e, 'CONDUCTOR', '#22d3ee'); }
  if (el.has('fire') && e.frozen > 0) {
    dmg += 40 * N.eff + base;
    e.frozen = 0; e.chill = 0; e.thawImm = e.d.heavy ? 5 : 2;
    combo = true; ignoreArmor = true;
    label(e, 'THERMAL SHOCK!', '#e879f9');
    burst(e.x, e.y - e.h / 2, '#f0abfc', 16, 220, 3);
  }
  if (e.mark > 0) dmg *= 1 + CREW.spotter.bonus;
  if (!ignoreArmor && e.armor > 0) {
    dmg = Math.max(1, dmg - (o.armorPierce ? e.armor * 0.5 : e.armor));
    if (dmg <= 2 && !o.noText) burst(o.px || e.x, o.py || e.y - e.h / 2, '#d4d4d8', 3, 120, 2, 0.25);
  }
  if (o.head) { N.heads++; Sfx.play('head'); }
  dealDamage(e, dmg, { ...o, combo });
  // statuses
  if (el.has('fire') && !combo) applyFire(e, base);
  if (el.has('cold')) applyCold(e);
  if (el.has('shock')) {
    if (!e.dead) e.stun = Math.max(e.stun, e.d.heavy ? 0.05 : 0.2);
    if (!o.chain) chain(e, base, o);
  }
  if (o.knock && !e.dead) e.kbV += o.knock * 4 * (e.d.heavy ? 0.25 : 1);
}

function dealDamage(e, dmg, o = {}) {
  if (e.dead) return;
  e.hp -= dmg;
  e.flash = 0.08;
  if (!o.noText) {
    const c = centerOf(e);
    const color = o.head ? '#fde047' : o.combo ? '#67e8f9' : '#ffffff';
    text(o.px || c.x, (o.py || c.y) - 10, Math.round(dmg).toString(), color, o.head ? 17 : 13 + Math.min(6, dmg / 25));
  }
  if (!o.noSound) Sfx.play('hit');
  if (e.hp <= 0) kill(e, o);
}

function dot(e, amount) {
  if (e.dead) return;
  e.hp -= amount;
  e.dotAcc += amount;
  if (Game.N.t - e.dotT > 0.5 && e.dotAcc >= 1) {
    const c = centerOf(e);
    text(c.x, c.y - 16, Math.round(e.dotAcc).toString(), '#fdba74', 12, 0.6);
    e.dotAcc = 0; e.dotT = Game.N.t;
  }
  if (e.hp <= 0) kill(e, {});
}

function applyFire(e, base) {
  if (e.dead) return;
  const N = Game.N;
  if (e.oiled > 0) { inferno(e, base); return; }
  if (e.wet) {
    e.wet = false; e.wetT = 0;
    label(e, 'STEAM', '#e5e7eb');
    burst(e.x, e.y - e.h * 0.6, 'rgba(230,230,240,0.6)', 8, 60, 6, 0.8, -60, 'smoke');
    dot(e, 3 * N.eff);
    return;
  }
  const dps = 4 * N.eff + 0.3 * base;
  e.burnDps = e.burn > 0 ? Math.max(e.burnDps, dps) : dps;
  e.burn = 3;
}

function inferno(e, base) {
  const N = Game.N;
  if (e.dead || e.oiled <= 0) return;
  e.oiled = 0; e.wet = false;
  e.burn = 4; e.burnDps = 3 * (4 * N.eff + 0.3 * Math.max(base, 8));
  label(e, 'INFERNO!', '#fb923c');
  burst(e.x, e.y - e.h / 2, '#fb923c', 18, 200, 4, 0.7, -100);
  shake(2);
  for (const o of N.enemies) if (o !== e && o.oiled > 0 && Math.abs(o.x - e.x) < 60 + o.w / 2) inferno(o, base);
}

function applyCold(e) {
  if (e.dead) return;
  if (e.burn > 0) { e.burn = 0; label(e, 'DOUSED', '#93c5fd'); burst(e.x, e.y - e.h / 2, 'rgba(220,230,255,0.6)', 6, 50, 5, 0.6, -50, 'smoke'); }
  const dur = d => e.d.heavy ? d * 0.5 : d;
  if (e.frozen > 0) { e.frozen = Math.max(e.frozen, dur(1.5)); return; }
  if (e.wet && e.thawImm <= 0) {
    e.frozen = dur(2); e.wet = false; e.wetT = 0; e.chill = 0;
    label(e, 'FLASH FREEZE', '#7dd3fc'); Sfx.play('freeze');
    burst(e.x, e.y - e.h / 2, '#e0f2fe', 10, 120, 3);
    return;
  }
  e.chillT = 2.5;
  e.chill = Math.min(3, e.chill + 1);
  if (e.chill >= 3 && e.thawImm <= 0) { e.frozen = dur(1.5); e.chill = 0; Sfx.play('freeze'); }
}

function chain(src, base, o) {
  const N = Game.N, from = centerOf(src);
  if (N.t - (src.chainT || -9) < 0.25) return;   // one chain per target per 0.25s, so pellets/SMG can't multiply it
  src.chainT = N.t;
  const n = src.wet ? 3 : 1, r = src.wet ? 180 : 110;
  const cands = N.enemies.filter(e => e !== src && !e.dead && distToBox(e, from.x, from.y) < r)
    .sort((a, b) => distToBox(a, from.x, from.y) - distToBox(b, from.x, from.y)).slice(0, n);
  for (const t of cands) {
    const c = centerOf(t);
    bolt(from.x, from.y, c.x, c.y, '#67e8f9', 2, 0.15);
    hit(t, base * 0.6, { elements: new Set(['shock']), chain: true, src: o.src, noSound: true });
  }
  if (cands.length) Sfx.play('zap');
}

function explode(x, y, r, dmg, o = {}) {
  const N = Game.N;
  for (const e of N.enemies) {
    if (e.dead || e === o.exclude) continue;
    const d = distToBox(e, x, y);
    if (d > r) continue;
    hit(e, dmg * (1 - 0.5 * d / r), { ...o, ignoreArmor: true, explosion: true, noSound: true });
  }
  burst(x, y, '#fdba74', Math.round(r / 3), r * 3.2, 4, 0.5, 200);
  burst(x, y, 'rgba(80,80,90,0.55)', Math.round(r / 6), r * 1.2, 10, 1.0, -40, 'smoke');
  N.fields.push({ type: 'flash', x, y, r, t: 0.15, max: 0.15 });
  shake(r / 22);
  Sfx.play('boom', r / 80);
}

function kill(e, o) {
  if (e.dead) return;
  const N = Game.N, run = Game.run;
  e.dead = true;
  const c = centerOf(e);
  const bounty = e.d.bounty * BALANCE.bountyScale(N.n) * ((o.src && o.src.salvage) || 1);
  if (bounty > 0 && !e.noBounty) {
    N.earned.bounty += bounty;
    text(c.x, c.y - e.h / 2 - 14, '+' + Math.round(bounty), '#fbbf24', 12, 0.9);
  }
  N.kills++;
  burst(c.x, c.y, rgb(hexRgb(e.d.color)), 12 + (e.d.boss ? 40 : 0), 200 + (e.d.boss ? 200 : 0), 3.5, 0.7);
  Sfx.play('kill');
  if (e.frozen > 0) {
    label(e, 'SHATTER', '#e0f2fe');
    burst(c.x, c.y, '#e0f2fe', 18, 260, 3, 0.6, 400, 'spark');
    for (const t of N.enemies) {
      if (t.dead || t === e || distToBox(t, c.x, c.y) > 60) continue;
      hit(t, 25 * N.eff, { ignoreArmor: true, noSound: true, combo: true });
      applyCold(t);
    }
  }
  if (e.id === 'puffer' && !e.popped) pufferPop(e, false);
  if (e.d.boss) {
    shake(16); N.slow = 1.2; Sfx.play('boom', 2);
    burst(c.x, c.y, '#fde68a', 60, 420, 5, 1.2);
    if (N.boss === e) N.boss = null;
  }
}

function pufferPop(e, atWall) {
  const N = Game.N;
  e.popped = true;
  const c = centerOf(e);
  burst(c.x, c.y, '#facc15', 20, 260, 3);
  if (atWall) {
    e.noBounty = true;
    hitWall(e.d.dmg, e);
    explode(c.x, c.y, 60, 20 * N.eff, { exclude: e });
    if (!e.dead) kill(e, {});
  } else {
    explode(c.x, c.y, 70, 40 * N.eff, { exclude: e });
    if (e.x < FIELD.wallFace + 80) hitWall(15, e);
  }
}

function hitWall(amount, e) {
  const N = Game.N, run = Game.run;
  const dmg = Math.max(1, amount * BALANCE.dmgScale(N.n) - run.fort.plating);
  run.wallHp = Math.max(0, run.wallHp - dmg);
  run.stats.wallDmgBy[e.id] = (run.stats.wallDmgBy[e.id] || 0) + dmg;
  N.wallFlash = 0.18; N.wallHitBy = e.d.name;
  shake(Math.min(8, 1 + dmg / 8));
  Sfx.play('wall');
  text(FIELD.wallFace - 10, e.y - e.lift - e.h * 0.8, '-' + Math.round(dmg), '#f87171', 14, 0.8);
  burst(FIELD.wallFace, e.y - e.lift - e.h * 0.5, '#9ca3af', 5, 120, 3, 0.5);
  if (run.fort.barbs > 0 && !e.dead && !e.d.flying) {
    dealDamage(e, 5 * run.fort.barbs * N.eff, { noSound: true });
  }
  if (run.wallHp <= 0 && !N.over) { N.over = true; N.overT = 0; N.slow = 1.5; shake(18); }
}

// ---- night update ------------------------------------------------------------
function updateNight(dt) {
  const N = Game.N, run = Game.run;
  N.t += dt;
  N.bannerT -= dt;
  N.shake *= Math.exp(-dt * 9);
  N.wallFlash -= dt;
  N.muzzle -= dt;
  if (N.overcharge > 0) N.overcharge -= dt;

  // spawns
  while (N.qi < N.queue.length && N.queue[N.qi].t <= N.t) {
    const s = N.queue[N.qi++];
    spawnEnemy(s.id, rand(1150, 1260));
  }

  // pearls
  N.pearlT -= dt;
  if (N.pearlT <= 0) {
    N.pearlT = rand(...BALANCE.pearlEvery) / (N.card.pearls || 1);
    N.pearls.push({ x: W + 30, y0: rand(90, 230), y: 0, t: Math.random() * 6, vx: -rand(80, 120) });
  }
  for (const p of N.pearls) { p.t += dt; p.x += p.vx * dt; p.y = p.y0 + Math.sin(p.t * 2) * 18; }
  N.pearls = N.pearls.filter(p => p.x > -40 && !p.got);

  updatePlayer(dt);
  updateCrew(dt);
  updateTraps(dt);
  for (const e of N.enemies) if (!e.dead) updateEnemy(e, dt);
  updateProjectiles(dt);
  updateFields(dt);
  updateFx(dt);
  N.enemies = N.enemies.filter(e => !e.dead);

  // end conditions
  if (N.over) {
    N.overT += dt;
    if (N.overT > 1.4) gameOver();
    return;
  }
  if (N.qi >= N.queue.length && N.enemies.length === 0) {
    N.endT += dt;
    if (N.endT > 1.6) finishNight();
  }
}

function aimDir() { const N = Game.N; return { x: Math.cos(N.aim), y: Math.sin(N.aim) }; }
function muzzle() {
  const N = Game.N, a = N.arms[N.cur], d = aimDir(), len = (a ? a.s.def.len : 22) + 12;
  return { x: FIELD.gunner.x + d.x * len, y: FIELD.gunner.y + d.y * len };
}

function updatePlayer(dt) {
  const N = Game.N, inp = Game.input, run = Game.run;
  N.aim = clamp(Math.atan2(inp.my - FIELD.gunner.y, inp.mx - FIELD.gunner.x), -1.4, 1.25);
  if (N.swapT > 0) N.swapT -= dt;
  const deck = crewLvl(run, 'deckhand');
  N.arms.forEach((a, i) => {
    a.recoil = Math.max(0, a.recoil - dt * 8);
    if (i !== N.cur) {
      a.cd = 0;
      if (deck > 0 && a.ammo < a.s.mag) {
        if (!a.reloading) { a.reloading = true; a.rt = 0; a.rTotal = a.s.reload; a.tried = true; }
        a.rt += dt / (CREW.deckhand.slow - CREW.deckhand.perLevel * (deck - 1));
        if (a.rt >= a.rTotal) finishReload(a, false);
      }
      return;
    }
    if (a.reloading) {
      a.rt += dt;
      if (a.rt >= a.rTotal) finishReload(a, false);
    }
  });
  const a = N.arms[N.cur];
  if (!a) return;
  a.cd -= dt;
  if (inp.tap > 0) inp.tap -= dt;
  if ((inp.down || inp.tap > 0) && N.swapT <= 0 && !a.reloading && !N.over) {
    if (a.ammo <= 0) startReload(a);
    else {
      const rate = a.s.rate * (N.overcharge > 0 ? 1.5 : 1);
      let guard = 0;
      while (a.cd <= 0 && a.ammo > 0 && guard++ < 6) {
        fire(a);
        inp.tap = 0;
        a.cd += 1 / rate;
        if (N.overcharge <= 0) a.ammo--;
      }
      if (a.ammo <= 0) startReload(a);
    }
  }
  if (a.cd < 0) a.cd = 0;
}

function startReload(a) {
  if (a.reloading || a.ammo >= a.s.mag) return;
  a.reloading = true; a.rt = 0; a.rTotal = a.s.reload; a.tried = false; a.jam = false; a.perfect = false;
  Sfx.play('reload');
}
function finishReload(a, perfect) {
  a.reloading = false; a.ammo = a.s.mag; a.perfect = perfect; a.jam = false;
  Sfx.play('reload');
}
function pressReload() {
  const N = Game.N, a = N && N.arms[N.cur];
  if (!a) return;
  if (!a.reloading) { startReload(a); return; }
  if (a.tried) return;
  a.tried = true;
  const p = a.rt / a.rTotal, z = BALANCE.perfectZone;
  const mp = muzzle();
  if (p >= z[0] && p <= z[1]) {
    finishReload(a, true);
    N.perfects++;
    text(Game.input.mx, Game.input.my + 40, 'PERFECT', '#86efac', 16, 0.8);
    Sfx.play('perfect');
  } else {
    a.rTotal += BALANCE.jamPenalty; a.jam = true;
    text(Game.input.mx, Game.input.my + 40, 'JAMMED', '#f87171', 15, 0.8);
    Sfx.play('jam');
  }
}
function switchTo(i) {
  const N = Game.N;
  if (!N || i === N.cur || !N.arms[i]) return;
  N.prev = N.cur; N.cur = i; N.swapT = BALANCE.swapTime;
  Sfx.play('click');
}

function fire(a) {
  const N = Game.N, s = a.s, mp = muzzle(), base = N.aim;
  a.recoil = 1;
  N.muzzle = 0.05;
  const perfect = a.perfect;
  if (s.kind === 'bullet') {
    for (let i = 0; i < s.pellets; i++) {
      const ang = base + rand(-s.spread, s.spread);
      N.bullets.push({
        x: mp.x, y: mp.y, vx: Math.cos(ang) * s.speed * rand(0.92, 1.05), vy: Math.sin(ang) * s.speed * rand(0.92, 1.05),
        s, dmg: s.dmg, pierce: s.pierce, hits: new Set(), dist: 0, perfect, thick: s.def.thick || 1.6,
      });
    }
    Sfx.play('shot', s.id === 'longrifle' || s.id === 'harpoon' ? 'heavy' : s.id === 'scattergun' ? 'heavy' : 'bullet');
    burst(mp.x, mp.y, '#fde68a', 3, 120, 2, 0.12, 0);
  } else if (s.kind === 'lob') {
    const tx = Game.input.mx, ty = Math.min(Game.input.my, FIELD.groundY);
    const T = clamp(Math.hypot(tx - mp.x, ty - mp.y) / 900, 0.45, 1.0), g = 900;
    N.shells.push({ x: mp.x, y: mp.y, vx: (tx - mp.x) / T, vy: (ty - mp.y - 0.5 * g * T * T) / T, g, t: 0, T, s, perfect });
    Sfx.play('shot', 'lob');
  } else if (s.kind === 'flame') {
    const ang = base + rand(-s.spread, s.spread), sp = s.speed * rand(0.85, 1.1);
    N.flames.push({ x: mp.x, y: mp.y, vx: Math.cos(ang) * sp, vy: Math.sin(ang) * sp, life: s.life, max: s.life, s, hits: new Set(), perfect });
    Sfx.play('shot', 'flame');
  } else if (s.kind === 'arc') {
    const d = aimDir();
    let best = null, bt = s.range;
    for (const e of N.enemies) {
      if (e.dead) continue;
      const t = rayBox(mp.x, mp.y, d.x, d.y, boxOf(e));
      if (t !== null && t < bt) { bt = t; best = e; }
    }
    const ex = mp.x + d.x * bt, ey = mp.y + d.y * bt;
    for (const p of N.pearls) if (segCircle(mp.x, mp.y, ex, ey, p.x, p.y, 18)) grabPearl(p);
    bolt(mp.x, mp.y, ex, ey, '#bae6fd', 2.5, 0.09);
    if (best) hit(best, s.dmg, { elements: s.elements, head: isHead(best, ex, ey), headMult: s.headMult, src: s, perfect, px: ex, py: ey });
    Sfx.play('shot', 'arc');
  }
}

function rayBox(ox, oy, dx, dy, b) {
  let tmin = 0, tmax = 1e9;
  for (const [o, d, lo, hi] of [[ox, dx, b.l, b.r], [oy, dy, b.t, b.b]]) {
    if (Math.abs(d) < 1e-6) { if (o < lo || o > hi) return null; continue; }
    let t1 = (lo - o) / d, t2 = (hi - o) / d;
    if (t1 > t2) [t1, t2] = [t2, t1];
    tmin = Math.max(tmin, t1); tmax = Math.min(tmax, t2);
    if (tmin > tmax) return null;
  }
  return tmin;
}
function segCircle(x1, y1, x2, y2, cx, cy, r) {
  const dx = x2 - x1, dy = y2 - y1, l2 = dx * dx + dy * dy || 1;
  const t = clamp(((cx - x1) * dx + (cy - y1) * dy) / l2, 0, 1);
  return Math.hypot(x1 + dx * t - cx, y1 + dy * t - cy) < r;
}
function grabPearl(p) {
  const N = Game.N;
  if (p.got) return;
  p.got = true;
  const v = BALANCE.pearlValue(N.n);
  N.earned.pearls += v; N.pearlsHit++;
  text(p.x, p.y - 20, '+' + v + ' PEARL', '#fde68a', 16, 1.2);
  burst(p.x, p.y, '#fde68a', 20, 200, 3, 0.8, 100, 'spark');
  Sfx.play('coin');
}

function updateProjectiles(dt) {
  const N = Game.N;
  // bullets (swept in small steps so fast rounds can't tunnel)
  for (const b of N.bullets) {
    const sp = Math.hypot(b.vx, b.vy) * dt, steps = Math.max(1, Math.ceil(sp / 9));
    const sx = b.vx * dt / steps, sy = b.vy * dt / steps;
    for (let i = 0; i < steps && !b.dead; i++) {
      b.px = b.x; b.py = b.y;
      b.x += sx; b.y += sy; b.dist += sp / steps;
      for (const p of N.pearls) if (Math.hypot(p.x - b.x, p.y - b.y) < 18) grabPearl(p);
      for (const e of N.enemies) {
        if (e.dead || b.hits.has(e) || !inBox(e, b.x, b.y)) continue;
        b.hits.add(e);
        let dmg = b.dmg;
        if (b.s.falloff && b.dist > b.s.falloff) dmg *= Math.max(0.35, 1 - (b.dist - b.s.falloff) / 600);
        const head = isHead(e, b.x, b.y);
        hit(e, dmg, { elements: b.s.elements, head, headMult: b.s.headMult, armorPierce: b.s.armorPierce, src: b.s, perfect: b.perfect, knock: b.s.knock, px: b.x, py: b.y });
        if (b.s.blast) explode(b.x, b.y, b.s.blast, dmg * 0.5, { elements: b.s.elements, src: b.s, exclude: e });
        burst(b.x, b.y, rgb(hexRgb(e.d.color), 1.2), 3, 90, 2, 0.25);
        if (b.pierce-- <= 0) { b.dead = true; break; }
      }
      if (b.y > FIELD.groundY + 4) {
        b.dead = true;
        burst(b.x, FIELD.groundY, b.x > FIELD.shoreX ? 'rgba(147,197,253,0.8)' : '#c8a96a', 4, 80, 2, 0.35);
      }
    }
    if (b.x > W + 50 || b.x < -50 || b.y < -50) b.dead = true;
  }
  N.bullets = N.bullets.filter(b => !b.dead);

  // mortar shells
  for (const sh of N.shells) {
    sh.t += dt;
    sh.vy += sh.g * dt;
    sh.x += sh.vx * dt; sh.y += sh.vy * dt;
    if (Math.random() < 0.6) N.parts.push({ x: sh.x, y: sh.y, vx: 0, vy: -10, life: 0.4, max: 0.4, color: 'rgba(160,160,170,0.5)', size: 4, grav: 0, kind: 'smoke' });
    let boom = sh.t >= sh.T || sh.y >= FIELD.groundY;
    if (!boom) for (const e of N.enemies) if (!e.dead && inBox(e, sh.x, sh.y)) { boom = true; break; }
    if (boom) {
      sh.dead = true;
      if (sh.gadget === 'frost') {
        explode(sh.x, sh.y, 110, 20 * N.eff, { elements: new Set(['cold']) });
        for (const e of N.enemies) if (!e.dead && distToBox(e, sh.x, sh.y) < 110) applyCold(e);
        burst(sh.x, sh.y, '#bae6fd', 30, 300, 3, 0.8, 100, 'spark');
        Sfx.play('freeze');
      } else {
        const dmg = sh.s.dmg * (sh.perfect ? 1 + BALANCE.perfectBonus : 1);
        explode(sh.x, sh.y, sh.s.splash, dmg, { elements: sh.s.elements, src: sh.s });
      }
    }
  }
  N.shells = N.shells.filter(s => !s.dead);

  // flame particles
  for (const f of N.flames) {
    f.life -= dt;
    f.x += f.vx * dt; f.y += f.vy * dt;
    f.vx *= Math.exp(-dt * 1.5); f.vy = f.vy * Math.exp(-dt * 1.5) - 40 * dt;
    if (f.y > FIELD.groundY) { f.y = FIELD.groundY; f.vy *= -0.3; }
    for (const e of N.enemies) {
      if (e.dead || f.hits.has(e) || distToBox(e, f.x, f.y) > 10) continue;
      f.hits.add(e);
      hit(e, f.s.dmg, { elements: f.s.elements, src: f.s, perfect: f.perfect, noSound: true, noText: Math.random() < 0.6 });
    }
  }
  N.flames = N.flames.filter(f => f.life > 0);
}

function updateEnemy(e, dt) {
  const N = Game.N, d = e.d;
  e.anim += dt;
  e.flash -= dt;
  if (e.mark > 0) e.mark -= dt;
  if (e.thawImm > 0) e.thawImm -= dt;
  if (e.stun > 0) e.stun -= dt;
  if (e.chillT > 0) { e.chillT -= dt; if (e.chillT <= 0) e.chill = 0; }
  if (e.frozen > 0) { e.frozen -= dt; if (e.frozen <= 0) e.thawImm = d.heavy ? 5 : 2; }
  if (e.burn > 0) {
    e.burn -= dt;
    dot(e, e.burnDps * dt);
    if (Math.random() < dt * 20) {
      const b = boxOf(e);
      N.parts.push({ x: rand(b.l, b.r), y: rand(b.t, b.b), vx: rand(-15, 15), vy: rand(-90, -40), life: 0.45, max: 0.45, color: Math.random() < 0.5 ? '#fb923c' : '#fde047', size: rand(2, 5), grav: -60, kind: 'dot' });
    }
    if (e.dead) return;
  }
  // wet / dry
  if (!d.flying) {
    const inSea = e.x >= FIELD.shoreX;
    if (inSea || N.card.wetAlways || e.inSpray) { e.wet = true; e.wetT = BALANCE.dryTime; }
    else if (e.wet && e.frozen <= 0) {
      e.wetT -= dt * (N.card.dryMult || 1);
      if (e.wetT <= 0) e.wet = false;
    }
    if (e.wet && Math.random() < dt * 6) {
      const b = boxOf(e);
      N.parts.push({ x: rand(b.l, b.r), y: rand(b.t + 4, b.b - 4), vx: 0, vy: 20, life: 0.4, max: 0.4, color: 'rgba(147,197,253,0.8)', size: 1.6, grav: 400, kind: 'dot' });
    }
  }
  if (e.oiled > 0 && !e.inOil) e.oiled -= dt;
  e.inSpray = false;
  // knockback
  if (Math.abs(e.kbV) > 1) { e.x = Math.min(W + 20, e.x + e.kbV * dt); e.kbV *= Math.exp(-dt * 6); }
  // emerging from the sea
  if (e.emerge > 0) {
    e.emerge -= dt;
    const total = d.boss === 'leviathan' ? 3 : d.boss === 'sirenqueen' ? 2 : 0.8;
    e.y = FIELD.groundY + e.h * 0.9 * Math.max(0, e.emerge / total);
    if (e.emerge <= 0) { e.y = FIELD.groundY; Sfx.play('splash'); burst(e.x, FIELD.groundY - 6, 'rgba(191,219,254,0.8)', 10, 120, 2, 0.5); }
  }
  const frozen = e.frozen > 0, stunned = e.stun > 0;
  let mult = 1;
  if (e.chillT > 0) mult *= 0.6;
  if (e.inOil) mult *= 0.65;
  if (frozen || stunned) mult = 0;
  e.inOil = false;
  const sp = e.speed * mult;
  const reach = FIELD.wallFace + e.w / 2 - 4;

  if (d.flying) return updateGull(e, dt, sp, mult);
  if (d.siren && !d.boss) {
    const others = N.enemies.some(o => !o.dead && !o.d.siren);
    const hold = N.qi < N.queue.length || others;
    if (hold && e.x > e.holdX) e.x -= sp * dt;
    else if (!hold) walk(e, dt, sp, reach);
    if (hold && e.x <= e.holdX + 2 && mult > 0) {
      e.timer -= dt;
      if (e.timer <= 0) { e.timer = 3.2; sing(e, 220, 0.12); }
    }
    return;
  }
  if (d.summon && e.emerge <= 0 && mult > 0) {
    // bosses summon adds; summoned adds give no bounty so bosses can't be farmed
    const enraged = d.summon.enraged && e.hp < e.maxHp * 0.5;
    const sm = enraged ? { ...d.summon, ...d.summon.enraged } : d.summon;
    e.timer2 -= dt;
    if (e.timer2 <= 0) {
      e.timer2 = sm.every;
      if (d.boss === 'leviathan') { Sfx.play('horn'); shake(4); }
      const alive = N.enemies.filter(o => !o.dead && o.summoned).length;
      for (let i = 0; i < Math.min(sm.n, (sm.cap || 99) - alive); i++) {
        const x = d.boss === 'crabking' ? e.x + rand(-30, 30) : rand(1110, 1250);
        const a = spawnEnemy(sm.id, x, { noEmerge: d.boss === 'crabking' });
        a.noBounty = true; a.summoned = true;
      }
    }
  }
  if (d.boss === 'sirenqueen') {
    if (e.emerge > 0 || mult === 0) return;
    e.timer -= dt;
    const enraged = e.hp < e.maxHp * 0.5;
    if (e.timer <= 0) { e.timer = enraged ? d.song.enragedEvery : d.song.every; sing(e, 9999, d.song.heal); }
    return;
  }
  if (d.leaps && mult > 0) {
    if (e.leapT >= 0) {
      e.leapT += dt;
      e.lift = Math.sin(Math.PI * e.leapT / 0.55) * 55;
      if (e.leapT >= 0.55) { e.leapT = -1; e.lift = 0; e.leapCd = rand(0.9, 1.8); }
    } else if (!e.attacking) {
      e.leapCd -= dt;
      if (e.leapCd <= 0) e.leapT = 0;
    }
  }
  if (e.id === 'puffer' && e.x <= reach + 2) { pufferPop(e, true); return; }
  walk(e, dt, sp * (e.leapT >= 0 ? 1.5 : 1), reach);
}

function walk(e, dt, sp, reach) {
  if (e.x > reach) { e.x = Math.max(reach, e.x - sp * dt); e.attacking = false; return; }
  e.attacking = true;
  if (sp <= 0 || e.d.atk <= 0) return;
  e.atkT -= dt;
  if (e.atkT <= 0) {
    e.atkT = 1 / e.d.atk;
    e.swing = 0.25;
    hitWall(e.d.dmg, e);
    if (e.d.boss === 'leviathan') { shake(10); Sfx.play('boom', 1.5); }
  }
}

function sing(e, radius, heal) {
  const N = Game.N;
  e.singT = 1;
  Sfx.play('sing');
  for (const o of N.enemies) {
    if (o.dead || o.d.flying) continue;
    if (Math.abs(o.x - e.x) > radius) continue;
    o.wet = true; o.wetT = BALANCE.dryTime;
    if (o !== e) o.hp = Math.min(o.maxHp, o.hp + o.maxHp * heal);
  }
  N.waves.push({ x: e.x, y: e.y - e.h * 0.6, r: 10, max: Math.min(radius, 900), t: 0 });
}

function updateGull(e, dt, sp, mult) {
  const N = Game.N;
  e.y = e.y + 0; // y is the hitbox bottom
  if (e.state === 'fly') {
    e.x -= sp * dt;
    e.y = e.alt + Math.sin(e.anim * 3) * 10;
    if (e.x < FIELD.wallFace + 210) { e.state = 'hover'; e.timer = 0.5; Sfx.play('gull'); }
  } else if (e.state === 'hover') {
    if (mult > 0) e.timer -= dt;
    e.y = e.alt + Math.sin(e.anim * 14) * 3;
    if (e.timer <= 0) e.state = 'dive';
  } else if (e.state === 'dive') {
    const tx = FIELD.wallFace + 14, ty = FIELD.wallTop + 25;
    const dx = tx - e.x, dy = ty - e.y, dist = Math.hypot(dx, dy);
    const s = sp * 2.3 * dt;
    if (dist <= s + 2) { hitWall(e.d.dmg, e); e.state = 'climb'; e.timer = 1.1; }
    else { e.x += dx / dist * s; e.y += dy / dist * s; }
  } else if (e.state === 'climb') {
    e.x += sp * 1.2 * dt; e.y -= sp * 0.9 * dt;
    e.timer -= dt;
    if (e.timer <= 0) { e.state = 'fly'; e.alt = clamp(e.y, 180, 340); }
  }
}

function updateCrew(dt) {
  const N = Game.N, run = Game.run;
  for (const c of N.crew) {
    c.flash -= dt;
    if (c.id === 'mechanic') {
      const mx = wallMax(run);
      if (run.wallHp < mx && !N.over) run.wallHp = Math.min(mx, run.wallHp + CREW.mechanic.repair * c.lvl * dt);
      continue;
    }
    if (c.id === 'scavenger' || c.id === 'deckhand') continue;
    c.cd -= dt;
    if (c.cd > 0) continue;
    if (c.id === 'rifleman') {
      let tgt = null;
      for (const e of N.enemies) if (!e.dead && e.x < W - 30 && e.emerge <= 0 && (!tgt || e.x < tgt.x)) tgt = e;
      if (!tgt) { c.cd = 0.3; continue; }
      const def = CREW.rifleman;
      c.cd = def.every;
      const ce = centerOf(tgt), ox = c.x + 14, oy = FIELD.wallTop - 22;
      c.aim = Math.atan2(ce.y - oy, ce.x - ox); c.flash = 0.06;
      const head = Math.random() < 0.2;
      N.bolts.push({ pts: [[ox, oy], [ce.x, ce.y]], color: 'rgba(254,240,138,0.8)', width: 1.4, life: 0.06, max: 0.06 });
      hit(tgt, def.dmg * (1 + def.perLevel * (c.lvl - 1)) * N.eff, { head, src: null, noSound: true });
      Sfx.play('shot', 'bullet');
    } else if (c.id === 'spotter') {
      c.cd = CREW.spotter.every - c.lvl;
      let tgt = null;
      for (const e of N.enemies) if (!e.dead && e.mark <= 0 && e.emerge <= 0 && (!tgt || e.hp > tgt.hp)) tgt = e;
      if (tgt) { tgt.mark = CREW.spotter.mark; c.flash = 0.3; const ce = centerOf(tgt); text(ce.x, ce.y - tgt.h, 'MARKED', '#f87171', 13, 0.9); }
      else c.cd = 0.5;
    }
  }
}

function updateTraps(dt) {
  const N = Game.N;
  for (const tr of N.traps) {
    const def = TRAPS[tr.id];
    tr.anim += dt;
    if (tr.id === 'post') {
      tr.cd -= dt;
      if (tr.cd > 0) continue;
      const ox = tr.x, oy = FIELD.groundY - 46;
      let tgt = null, bd = def.range;
      for (const e of N.enemies) { if (e.dead) continue; const d = distToBox(e, ox, oy); if (d < bd) { bd = d; tgt = e; } }
      if (tgt) {
        tr.cd = def.every;
        const c = centerOf(tgt);
        bolt(ox, oy, c.x, c.y, '#a5f3fc', 2, 0.12);
        hit(tgt, def.dmg * N.eff, { elements: new Set(['shock']), noSound: true });
        Sfx.play('zap');
      } else tr.cd = 0.2;
      continue;
    }
    for (const e of N.enemies) {
      if (e.dead || !grounded(e)) continue;
      if (Math.abs(e.x - tr.x) > def.w / 2 + e.w * 0.3) continue;
      if (tr.id === 'spikes') dot(e, def.dps * N.eff * dt);
      else if (tr.id === 'oil') { e.inOil = true; e.oiled = 5; }
      else if (tr.id === 'sprinkler') e.inSpray = true;
      else if (tr.id === 'mine' && tr.armed) {
        tr.armed = false;
        explode(tr.x, FIELD.groundY - 12, def.radius, def.dmg * N.eff, {});
        break;
      }
    }
  }
}

// ---- gadgets -----------------------------------------------------------------
function useGadget(i) {
  const N = Game.N;
  if (!N || N.over) return;
  const g = N.gadgets[i];
  if (!g || g.cd > 0) { if (g) Sfx.play('deny'); return; }
  const def = GADGETS[g.id], mx = Game.input.mx, my = Math.min(Game.input.my, FIELD.groundY);
  g.cd = def.cd;
  if (g.id === 'flare') {
    N.fields.push({ type: 'fire', x: mx, y: FIELD.groundY, r: 85, t: 5, max: 5, tick: 0 });
    Sfx.play('boom', 0.5);
  } else if (g.id === 'frost') {
    const mp = muzzle(), T = 0.45, gr = 900;
    N.shells.push({ x: mp.x, y: mp.y, vx: (mx - mp.x) / T, vy: (my - mp.y - 0.5 * gr * T * T) / T, g: gr, t: 0, T, gadget: 'frost' });
  } else if (g.id === 'storm') {
    let k = 0;
    for (const e of N.enemies) if (!e.dead && e.wet) N.fields.push({ type: 'strike', target: e, t: 0.05 + 0.06 * k++, max: 1 });
    if (!k) text(mx, my - 20, 'NO WET TARGETS', '#94a3b8', 14, 0.9);
    shake(3);
  } else if (g.id === 'depth') {
    N.fields.push({ type: 'charge', x: mx, y: my, t: 0.7, max: 0.7 });
  } else if (g.id === 'surge') {
    N.waves.push({ surge: true, x: FIELD.wallFace, hit: new Set() });
    Sfx.play('splash');
  } else if (g.id === 'overcharge') {
    N.overcharge = 5;
    N.arms.forEach(a => { if (a.reloading) finishReload(a, false); });
    text(FIELD.gunner.x, FIELD.gunner.y - 50, 'OVERCHARGE', '#fde047', 18, 1.2);
    Sfx.play('perfect');
  }
}

function updateFields(dt) {
  const N = Game.N;
  for (const g of N.gadgets) if (g && g.cd > 0) g.cd -= dt;
  for (const f of N.fields) {
    f.t -= dt;
    if (f.type === 'fire') {
      f.tick -= dt;
      if (Math.random() < dt * 40) N.parts.push({ x: f.x + rand(-f.r, f.r), y: FIELD.groundY - rand(0, 8), vx: rand(-10, 10), vy: rand(-120, -50), life: 0.5, max: 0.5, color: Math.random() < 0.5 ? '#f97316' : '#fde047', size: rand(3, 6), grav: -50, kind: 'dot' });
      if (f.tick <= 0) {
        f.tick = 0.4;
        for (const e of N.enemies) if (!e.dead && grounded(e) && Math.abs(e.x - f.x) < f.r + e.w / 2) hit(e, 4 * N.eff, { elements: new Set(['fire']), ignoreArmor: true, noSound: true, noText: true });
      }
    } else if (f.type === 'strike' && f.t <= 0 && !f.done) {
      f.done = true;
      const e = f.target;
      if (!e.dead) {
        const c = centerOf(e);
        bolt(c.x + rand(-30, 30), 0, c.x, c.y, '#e0f2fe', 3, 0.2);
        hit(e, 30 * N.eff, { elements: new Set(['shock']), chain: true, ignoreArmor: true });
        Sfx.play('zap');
      }
    } else if (f.type === 'charge' && f.t <= 0 && !f.done) {
      f.done = true;
      explode(f.x, f.y, 120, 120 * N.eff, { knock: 60 });
      for (const e of N.enemies) if (!e.dead && distToBox(e, f.x, f.y) < 120) e.kbV += (e.x > f.x ? 1 : -0.3) * 200 * (e.d.heavy ? 0.25 : 1);
    }
  }
  N.fields = N.fields.filter(f => f.t > 0 || (f.type === 'strike' && !f.done) || (f.type === 'charge' && !f.done));
  for (const w of N.waves) {
    if (w.surge) {
      w.x += 1000 * dt;
      for (const e of N.enemies) {
        if (e.dead || e.d.flying || w.hit.has(e) || e.x > w.x) continue;
        w.hit.add(e);
        e.wet = true; e.wetT = BALANCE.dryTime;
        if (e.burn > 0) e.burn = 0;
        e.kbV += 260 * (e.d.heavy ? 0.3 : 1);
      }
      w.dead = w.x > FIELD.shoreX + 100;
    } else {
      w.t += dt; w.r += 500 * dt;
      w.dead = w.r > w.max;
    }
  }
  N.waves = N.waves.filter(w => !w.dead);
}

function updateFx(dt) {
  const N = Game.N;
  for (const p of N.parts) {
    p.life -= dt;
    p.vy += p.grav * dt;
    p.x += p.vx * dt; p.y += p.vy * dt;
    if (p.kind !== 'smoke' && p.y > FIELD.groundY + 6 && p.vy > 0) { p.y = FIELD.groundY + 6; p.vy *= -0.3; p.vx *= 0.6; }
  }
  N.parts = N.parts.filter(p => p.life > 0);
  for (const t of N.texts) { t.life -= dt; t.y += t.vy * dt; t.vy *= Math.exp(-dt * 2); }
  N.texts = N.texts.filter(t => t.life > 0);
  for (const b of N.bolts) b.life -= dt;
  N.bolts = N.bolts.filter(b => b.life > 0);
  for (const e of N.enemies) { if (e.singT > 0) e.singT -= dt; if (e.swing > 0) e.swing -= dt; }
}

// ---- night end / game over -----------------------------------------------------
function finishNight() {
  const N = Game.N, run = Game.run;
  const mx = wallMax(run), pct = run.wallHp / mx;
  const bounty = Math.round(N.earned.bounty);
  const bonus = Math.round(BALANCE.nightBonus(N.n) * (0.5 + 0.5 * pct));
  const pearls = N.earned.pearls;
  const sub = bounty + bonus + pearls;
  const scavLvl = crewLvl(run, 'scavenger');
  const scav = Math.round(sub * CREW.scavenger.cut * scavLvl);
  const total = Math.round((sub + scav) * N.card.mult);
  const mech = crewLvl(run, 'mechanic');
  const repaired = Math.round(Math.min(mx - run.wallHp, mx * CREW.mechanic.after * mech));
  run.wallHp += repaired;
  run.scrap += total;
  run.stats.kills += N.kills; run.stats.heads += N.heads; run.stats.perfects += N.perfects; run.stats.pearls += N.pearlsHit; run.stats.earned += total;
  const summary = { n: N.n, card: N.card, kills: N.kills, heads: N.heads, perfects: N.perfects, pearlsHit: N.pearlsHit, bounty, bonus, pct, pearls, scav, total, repaired, wallLost: Math.round(N.startHp - (run.wallHp - repaired)) };
  run.night++;
  if (run.night - 1 > bestNight()) Store.set('tidewall.best', run.night - 1);
  Game.N = null;
  Sfx.play('dawn');
  saveRun();
  if (summary.n === BALANCE.victoryNight && !run.endless) UI.showVictory(summary);
  else UI.showSummary(summary);
}

function gameOver() {
  const run = Game.run, N = Game.N;
  const by = Object.entries(run.stats.wallDmgBy).sort((a, b) => b[1] - a[1])[0];
  const info = {
    night: N.n, kills: run.stats.kills + N.kills, heads: run.stats.heads + N.heads, perfects: run.stats.perfects + N.perfects,
    earned: run.stats.earned, cause: by ? ENEMIES[by[0]].name : '—', causeDmg: by ? Math.round(by[1]) : 0, last: N.wallHitBy,
  };
  if (N.n - 1 > bestNight()) Store.set('tidewall.best', N.n - 1);
  Store.del('tidewall.run');
  Game.N = null;
  Game.state = 'gameover';
  UI.showGameOver(info);
}

// ---- rendering -------------------------------------------------------------------
const STARS = Array.from({ length: 90 }, () => ({ x: Math.random() * W, y: Math.random() * 380, r: Math.random() * 1.4 + 0.3, p: Math.random() * 6 }));
const CRACKS = Array.from({ length: 14 }, (_, i) => {
  const pts = [[rand(FIELD.wallLeft + 10, FIELD.wallFace - 10), rand(FIELD.wallTop + 10, FIELD.groundY + 60)]];
  for (let k = 0; k < 4; k++) { const l = pts[pts.length - 1]; pts.push([l[0] + rand(-18, 18), l[1] + rand(6, 22)]); }
  return { pts, at: 1 - (i + 1) / 15 };
});

function render(ctx) {
  const N = Game.N, t = Game.time;
  ctx.save();
  if (N && N.shake > 0.2) ctx.translate(rand(-N.shake, N.shake), rand(-N.shake, N.shake));
  drawSky(ctx, t);
  drawGround(ctx, t);
  const traps = N ? N.traps : (Game.run ? Game.run.placed : []);
  for (const tr of traps) drawTrap(ctx, tr, t);
  if (Game.state === 'place' && Game.placeSel) drawGhost(ctx);
  if (N) for (const f of N.fields) drawField(ctx, f, t);
  drawWall(ctx);
  drawCrew(ctx, t);
  drawGunner(ctx, t);
  if (N) {
    for (const p of N.pearls) drawPearl(ctx, p, t);
    const list = N.enemies.slice().sort((a, b) => (a.d.flying ? 1 : 0) - (b.d.flying ? 1 : 0) || b.x - a.x);
    for (const e of list) drawEnemy(ctx, e, t);
  }
  drawSeaFront(ctx, t);
  if (N) {
    for (const w of N.waves) drawWave(ctx, w);
    drawShots(ctx);
    drawParts(ctx);
    if (N.card.fog) drawFog(ctx, t);
    for (const e of N.enemies) if (e.mark > 0) drawMark(ctx, e, t);
    drawTexts(ctx);
  }
  ctx.restore();
  if (N) drawHud(ctx, t);
  if (N || Game.state === 'place') drawCrosshair(ctx);
}

function drawSky(ctx, t) {
  const g = ctx.createLinearGradient(0, 0, 0, 480);
  g.addColorStop(0, '#070b1d'); g.addColorStop(0.6, '#16213f'); g.addColorStop(1, '#3a3456');
  ctx.fillStyle = g; ctx.fillRect(0, 0, W, 480);
  for (const s of STARS) {
    ctx.globalAlpha = 0.4 + 0.4 * Math.sin(t * 1.3 + s.p);
    ctx.fillStyle = '#fff'; ctx.fillRect(s.x, s.y, s.r, s.r);
  }
  ctx.globalAlpha = 1;
  const mg = ctx.createRadialGradient(930, 120, 20, 930, 120, 160);
  mg.addColorStop(0, 'rgba(255,248,220,0.35)'); mg.addColorStop(1, 'rgba(255,248,220,0)');
  ctx.fillStyle = mg; ctx.fillRect(760, 0, 340, 300);
  ctx.fillStyle = '#fdf6dc'; ctx.beginPath(); ctx.arc(930, 120, 46, 0, Math.PI * 2); ctx.fill();
  ctx.fillStyle = 'rgba(200,190,160,0.35)';
  [[915, 105, 9], [945, 132, 7], [925, 140, 5], [948, 102, 4]].forEach(([x, y, r]) => { ctx.beginPath(); ctx.arc(x, y, r, 0, Math.PI * 2); ctx.fill(); });
  // distant sea band
  const sg = ctx.createLinearGradient(0, 470, 0, 610);
  sg.addColorStop(0, '#1b2d52'); sg.addColorStop(1, '#0f1d3a');
  ctx.fillStyle = sg; ctx.fillRect(0, 470, W, 140);
  ctx.fillStyle = 'rgba(253,246,220,0.25)';
  for (let i = 0; i < 9; i++) {
    const y = 478 + i * 12, w = 60 - i * 4 + Math.sin(t * 2 + i) * 8;
    ctx.fillRect(930 - w / 2 + Math.sin(t + i * 2) * 6, y, w, 2);
  }
  // headland silhouette
  ctx.fillStyle = '#0b1328';
  ctx.beginPath(); ctx.moveTo(0, 470); ctx.lineTo(0, 400); ctx.quadraticCurveTo(120, 380, 260, 440); ctx.lineTo(420, 470); ctx.fill();
}

function drawGround(ctx, t) {
  const gy = FIELD.groundY;
  const g = ctx.createLinearGradient(FIELD.wallFace, 0, FIELD.shoreX, 0);
  g.addColorStop(0, '#6f5a3e'); g.addColorStop(0.55, '#5f4c35'); g.addColorStop(1, '#3d3226');
  ctx.fillStyle = g;
  ctx.beginPath(); ctx.moveTo(0, gy); ctx.lineTo(FIELD.shoreX + 40, gy); ctx.lineTo(FIELD.shoreX + 140, H); ctx.lineTo(0, H); ctx.fill();
  ctx.fillStyle = 'rgba(0,0,0,0.18)';
  for (let x = 220; x < FIELD.shoreX; x += 37) ctx.fillRect(x + ((x * 7) % 13), gy + 14 + ((x * 3) % 40), 6, 2);
  ctx.fillStyle = 'rgba(255,240,200,0.12)'; ctx.fillRect(0, gy, FIELD.shoreX + 40, 2);
}

function drawSeaFront(ctx, t) {
  const gy = FIELD.groundY, sx = FIELD.shoreX;
  const N = Game.N;
  ctx.fillStyle = 'rgba(30,64,120,0.78)';
  ctx.beginPath();
  ctx.moveTo(sx - 10 + Math.sin(t * 1.5) * 12, H);
  for (let x = sx - 10; x <= W; x += 16) ctx.lineTo(x + Math.sin(t * 1.5) * 12 * (1 - (x - sx) / 300), gy - 12 + Math.sin(x * 0.04 + t * 2.2) * 4);
  ctx.lineTo(W, H); ctx.fill();
  ctx.strokeStyle = 'rgba(219,234,254,0.55)'; ctx.lineWidth = 2;
  ctx.beginPath();
  for (let x = sx - 10; x <= W; x += 16) {
    const y = gy - 12 + Math.sin(x * 0.04 + t * 2.2) * 4;
    if (x === sx - 10) ctx.moveTo(x + Math.sin(t * 1.5) * 12, y); else ctx.lineTo(x + Math.sin(t * 1.5) * 12 * (1 - (x - sx) / 300), y);
  }
  ctx.stroke();
  if (N && N.card.wetAlways) {
    ctx.strokeStyle = 'rgba(148,163,184,0.35)'; ctx.lineWidth = 1;
    ctx.beginPath();
    for (let i = 0; i < 70; i++) {
      const x = (i * 97 + t * 300) % W, y = (i * 53 + t * 700) % H;
      ctx.moveTo(x, y); ctx.lineTo(x - 4, y + 14);
    }
    ctx.stroke();
  }
}

function drawWall(ctx) {
  const run = Game.run, N = Game.N;
  const l = FIELD.wallLeft, r = FIELD.wallFace, top = FIELD.wallTop;
  ctx.fillStyle = '#3f3f46'; ctx.fillRect(l, top, r - l, H - top);
  const bw = 38, bh = 22;
  for (let row = 0, y = top; y < H; row++, y += bh) {
    for (let x = l - (row % 2 ? bw / 2 : 0); x < r; x += bw) {
      const x0 = Math.max(l, x), x1 = Math.min(r, x + bw);
      ctx.fillStyle = ((row * 7 + x) % 3) ? '#57534e' : '#525050';
      ctx.fillRect(x0 + 1, y + 1, x1 - x0 - 2, bh - 2);
    }
  }
  // crenellations
  ctx.fillStyle = '#5b5755';
  for (let x = l; x < r - 10; x += 34) ctx.fillRect(x, top - 14, 20, 14);
  if (run) {
    const pct = run.wallHp / wallMax(run);
    ctx.strokeStyle = 'rgba(15,15,20,0.85)'; ctx.lineWidth = 2;
    for (const c of CRACKS) {
      if (pct > c.at) continue;
      ctx.beginPath(); ctx.moveTo(...c.pts[0]); c.pts.slice(1).forEach(p => ctx.lineTo(...p)); ctx.stroke();
    }
    if (run.fort.plating > 0) {
      ctx.fillStyle = '#71717a';
      for (let i = 0; i < run.fort.plating; i++) ctx.fillRect(r - 8, top + 10 + i * 34, 8, 28);
      ctx.fillStyle = '#d4d4d8';
      for (let i = 0; i < run.fort.plating; i++) { ctx.fillRect(r - 5, top + 14 + i * 34, 2, 2); ctx.fillRect(r - 5, top + 32 + i * 34, 2, 2); }
    }
    if (run.fort.barbs > 0) {
      ctx.fillStyle = '#a1a1aa';
      const n = 3 + run.fort.barbs * 2;
      for (let i = 0; i < n; i++) {
        const y = top + 20 + i * ((FIELD.groundY - top - 30) / n);
        ctx.beginPath(); ctx.moveTo(r, y); ctx.lineTo(r + 14, y + 4); ctx.lineTo(r, y + 8); ctx.fill();
      }
    }
  }
  if (N && N.wallFlash > 0) { ctx.fillStyle = `rgba(248,113,113,${N.wallFlash * 2.5})`; ctx.fillRect(l, top - 14, r - l, H - top + 14); }
}

function drawPerson(ctx, x, footY, coat, scale = 1, t = 0) {
  ctx.save(); ctx.translate(x, footY); ctx.scale(scale, scale);
  ctx.fillStyle = '#1f2937'; ctx.fillRect(-7, -16, 5, 16); ctx.fillRect(2, -16, 5, 16);
  ctx.fillStyle = coat; ctx.fillRect(-9, -40, 18, 26);
  ctx.fillStyle = '#e7c9a9'; ctx.beginPath(); ctx.arc(0, -48, 7, 0, Math.PI * 2); ctx.fill();
  ctx.fillStyle = '#292524'; ctx.fillRect(-8, -56, 16, 5);
  ctx.restore();
}

function drawCrew(ctx, t) {
  const run = Game.run; if (!run) return;
  const N = Game.N;
  const list = N ? N.crew : run.crew.map((c, i) => ({ id: c.id, lvl: c.lvl, x: FIELD.crewX[i], aim: 0, flash: 0 }));
  for (const c of list) {
    const def = CREW[c.id];
    drawPerson(ctx, c.x, FIELD.wallTop, def.color, 0.8, t);
    const hy = FIELD.wallTop - 26;
    if (c.id === 'rifleman') {
      ctx.save(); ctx.translate(c.x + 2, hy); ctx.rotate(c.aim || 0);
      ctx.fillStyle = '#3f3f46'; ctx.fillRect(0, -2, 26, 4);
      if (c.flash > 0) { ctx.fillStyle = '#fde68a'; ctx.beginPath(); ctx.arc(28, 0, 5, 0, Math.PI * 2); ctx.fill(); }
      ctx.restore();
    } else if (c.id === 'mechanic') {
      const sw = Math.sin(t * 8) * 0.6;
      ctx.save(); ctx.translate(c.x + 4, hy); ctx.rotate(0.6 + sw); ctx.fillStyle = '#a1a1aa'; ctx.fillRect(0, -2, 14, 3); ctx.fillRect(12, -5, 4, 9); ctx.restore();
      if (N && run.wallHp < wallMax(run) && Math.random() < 0.1) N.parts.push({ x: c.x + 14, y: hy, vx: rand(-40, 40), vy: rand(-80, -20), life: 0.3, max: 0.3, color: '#fde047', size: 1.5, grav: 300, kind: 'dot' });
    } else if (c.id === 'spotter') {
      ctx.fillStyle = c.flash > 0 ? '#fca5a5' : '#52525b'; ctx.fillRect(c.x - 2, hy - 18, 12, 5);
    } else if (c.id === 'scavenger') {
      ctx.fillStyle = '#78350f'; ctx.fillRect(c.x - 12, hy - 4, 8, 12);
    } else if (c.id === 'deckhand') {
      ctx.fillStyle = '#facc15'; ctx.fillRect(c.x + 6, hy + 2, 8, 6);
    }
    ctx.fillStyle = 'rgba(255,255,255,0.7)'; ctx.font = '9px sans-serif'; ctx.textAlign = 'center';
    ctx.fillText('★'.repeat(c.lvl), c.x, FIELD.wallTop + 12);
  }
}

function drawGunner(ctx, t) {
  const N = Game.N, g = FIELD.gunner;
  drawPerson(ctx, g.x, FIELD.wallTop, '#1d4ed8', 1, t);
  const aim = N ? N.aim : clamp(Math.atan2(Game.input.my - g.y, Game.input.mx - g.x), -1.4, 1.25);
  const a = N && N.arms[N.cur];
  const def = a ? a.s.def : WEAPONS[(Game.run && Game.run.loadout.weapons[0]) || 'pistol'];
  const rec = a ? a.recoil * 5 : 0;
  ctx.save(); ctx.translate(g.x, g.y); ctx.rotate(aim);
  ctx.fillStyle = '#1e40af'; ctx.fillRect(-2 - rec, -3, 14, 6);
  ctx.fillStyle = def.body; ctx.fillRect(10 - rec, -4, def.len, 7);
  ctx.fillStyle = '#111'; ctx.fillRect(12 - rec, 3, 5, 6);
  if (def.kind === 'arc') { ctx.fillStyle = '#7dd3fc'; ctx.fillRect(10 + def.len - 6 - rec, -6, 6, 11); }
  if (def.kind === 'flame') { ctx.fillStyle = '#f97316'; ctx.beginPath(); ctx.arc(10 + def.len - rec, 0, 3, 0, Math.PI * 2); ctx.fill(); }
  if (def.kind === 'lob') { ctx.fillStyle = '#52525b'; ctx.fillRect(10 + def.len - 8 - rec, -6, 8, 11); }
  if (N && N.muzzle > 0 && def.kind === 'bullet') {
    ctx.fillStyle = '#fde68a'; ctx.beginPath(); ctx.moveTo(10 + def.len, -6); ctx.lineTo(10 + def.len + 16, 0); ctx.lineTo(10 + def.len, 6); ctx.fill();
  }
  ctx.restore();
  if (N && N.overcharge > 0) {
    ctx.strokeStyle = `rgba(253,224,71,${0.4 + 0.3 * Math.sin(t * 20)})`; ctx.lineWidth = 2;
    ctx.beginPath(); ctx.arc(g.x, g.y - 6, 30, 0, Math.PI * 2); ctx.stroke();
  }
}

function drawTrap(ctx, tr, t) {
  const def = TRAPS[tr.id], gy = FIELD.groundY, x = tr.x;
  if (tr.id === 'spikes') {
    ctx.fillStyle = '#3f3f46'; ctx.fillRect(x - def.w / 2, gy - 3, def.w, 4);
    ctx.fillStyle = '#d4d4d8';
    for (let i = 0; i < 9; i++) { const sx = x - def.w / 2 + 4 + i * (def.w - 8) / 8; ctx.beginPath(); ctx.moveTo(sx - 3, gy - 2); ctx.lineTo(sx, gy - 11); ctx.lineTo(sx + 3, gy - 2); ctx.fill(); }
  } else if (tr.id === 'oil') {
    ctx.fillStyle = 'rgba(10,10,14,0.85)'; ctx.beginPath(); ctx.ellipse(x, gy + 3, def.w / 2, 6, 0, 0, Math.PI * 2); ctx.fill();
    ctx.fillStyle = 'rgba(167,139,250,0.25)'; ctx.beginPath(); ctx.ellipse(x - 10, gy + 1, def.w / 4, 2, 0, 0, Math.PI * 2); ctx.fill();
  } else if (tr.id === 'mine') {
    const armed = tr.armed !== false;
    ctx.fillStyle = '#27272a'; ctx.beginPath(); ctx.arc(x, gy - 4, 10, Math.PI, 0); ctx.fill();
    ctx.fillStyle = armed ? (Math.sin(t * 6) > 0 ? '#ef4444' : '#7f1d1d') : '#3f3f46'; ctx.fillRect(x - 2, gy - 16, 4, 5);
  } else if (tr.id === 'post') {
    ctx.fillStyle = '#44403c'; ctx.fillRect(x - 3, gy - 46, 6, 46);
    ctx.fillStyle = '#7dd3fc'; ctx.beginPath(); ctx.arc(x, gy - 48, 6, 0, Math.PI * 2); ctx.fill();
    ctx.strokeStyle = `rgba(125,211,252,${0.3 + 0.3 * Math.sin(t * 9 + x)})`; ctx.beginPath(); ctx.arc(x, gy - 48, 10, 0, Math.PI * 2); ctx.stroke();
  } else if (tr.id === 'sprinkler') {
    ctx.fillStyle = '#334155'; ctx.fillRect(x - 5, gy - 14, 10, 14);
    ctx.fillStyle = 'rgba(147,197,253,0.55)';
    for (let i = 0; i < 14; i++) {
      const p = ((t * 1.4 + i / 14) % 1), dir = i % 2 ? 1 : -1;
      const px = x + dir * p * def.w / 2, py = gy - 16 - Math.sin(p * Math.PI) * 40;
      ctx.fillRect(px, py, 2, 2);
    }
  }
}

function drawGhost(ctx) {
  const id = Game.placeSel, def = TRAPS[id];
  const x = clamp(Game.input.mx, FIELD.trapMinX, FIELD.trapMaxX);
  ctx.globalAlpha = 0.5; drawTrap(ctx, { id, x, armed: true }, Game.time); ctx.globalAlpha = 1;
  ctx.strokeStyle = 'rgba(255,255,255,0.4)'; ctx.setLineDash([4, 4]);
  ctx.strokeRect(x - def.w / 2, FIELD.groundY - 50, def.w, 60); ctx.setLineDash([]);
}

function drawField(ctx, f, t) {
  if (f.type === 'fire') {
    const a = Math.min(1, f.t / 0.5);
    const g = ctx.createRadialGradient(f.x, FIELD.groundY, 5, f.x, FIELD.groundY, f.r);
    g.addColorStop(0, `rgba(251,146,60,${0.5 * a})`); g.addColorStop(1, 'rgba(251,146,60,0)');
    ctx.fillStyle = g; ctx.fillRect(f.x - f.r, FIELD.groundY - f.r, f.r * 2, f.r + 10);
  } else if (f.type === 'charge') {
    ctx.strokeStyle = `rgba(248,113,113,${0.5 + 0.5 * Math.sin(t * 30)})`; ctx.lineWidth = 2;
    ctx.beginPath(); ctx.arc(f.x, f.y, 120 * (1 - f.t / f.max) + 10, 0, Math.PI * 2); ctx.stroke();
    ctx.fillStyle = '#27272a'; ctx.beginPath(); ctx.arc(f.x, f.y, 8, 0, Math.PI * 2); ctx.fill();
  } else if (f.type === 'flash') {
    ctx.fillStyle = `rgba(255,237,180,${0.6 * f.t / f.max})`;
    ctx.beginPath(); ctx.arc(f.x, f.y, f.r, 0, Math.PI * 2); ctx.fill();
  }
}

function drawWave(ctx, w) {
  if (w.surge) {
    const g = ctx.createLinearGradient(w.x - 80, 0, w.x, 0);
    g.addColorStop(0, 'rgba(96,165,250,0)'); g.addColorStop(1, 'rgba(147,197,253,0.65)');
    ctx.fillStyle = g; ctx.fillRect(w.x - 80, FIELD.groundY - 70, 80, 80);
  } else {
    ctx.strokeStyle = `rgba(94,234,212,${0.6 * (1 - w.r / w.max)})`; ctx.lineWidth = 3;
    ctx.beginPath(); ctx.arc(w.x, w.y, w.r, 0, Math.PI * 2); ctx.stroke();
  }
}

function drawPearl(ctx, p, t) {
  const g = ctx.createRadialGradient(p.x - 4, p.y - 4, 2, p.x, p.y, 16);
  g.addColorStop(0, '#fffbeb'); g.addColorStop(0.5, '#fcd34d'); g.addColorStop(1, 'rgba(252,211,77,0.2)');
  ctx.fillStyle = g; ctx.beginPath(); ctx.arc(p.x, p.y, 14, 0, Math.PI * 2); ctx.fill();
  ctx.strokeStyle = 'rgba(255,255,255,0.5)'; ctx.lineWidth = 1.5; ctx.beginPath(); ctx.arc(p.x, p.y, 17 + Math.sin(t * 5) * 2, 0, Math.PI * 2); ctx.stroke();
}

function tintOf(e) {
  const c = hexRgb(e.d.color).slice();
  if (e.wet) mixInto(c, '#1e3a8a', 0.3);
  if (e.oiled > 0) mixInto(c, '#0a0a0a', 0.55);
  if (e.chillT > 0) mixInto(c, '#93c5fd', 0.45);
  if (e.frozen > 0) mixInto(c, '#dbeafe', 0.7);
  if (e.burn > 0) mixInto(c, '#ff6a00', 0.2);
  if (e.flash > 0) mixInto(c, '#ffffff', 0.75);
  return c;
}

function drawEnemy(ctx, e, t) {
  const b = boxOf(e), c = tintOf(e), w = e.w, h = e.h, x = e.x, a = e.anim;
  const frz = e.frozen > 0 ? 0 : 1;
  const step = Math.sin(a * 9 * frz) * (e.attacking ? 0.3 : 1);
  ctx.save();
  if (e.emerge > 0 && !e.d.flying) {
    ctx.beginPath(); ctx.rect(b.l - 40, 0, w + 80, FIELD.groundY - 2); ctx.clip();
  }
  const D = DRAW[e.d.boss || e.id];
  if (D) D(ctx, e, b, c, step, t);
  ctx.restore();
  if (e.frozen > 0) {
    ctx.fillStyle = 'rgba(219,234,254,0.35)'; ctx.strokeStyle = 'rgba(255,255,255,0.8)'; ctx.lineWidth = 1.5;
    ctx.beginPath(); ctx.moveTo(b.l - 4, b.b); ctx.lineTo(b.l - 2, b.t - 4); ctx.lineTo(x, b.t - 10); ctx.lineTo(b.r + 2, b.t - 2); ctx.lineTo(b.r + 4, b.b); ctx.closePath(); ctx.fill(); ctx.stroke();
  }
  if (e.wet && e.frozen <= 0) {
    ctx.strokeStyle = 'rgba(147,197,253,0.5)'; ctx.lineWidth = 1;
    ctx.beginPath(); ctx.moveTo(b.l + 3, b.t + 6); ctx.lineTo(b.l + w * 0.4, b.t + 3); ctx.stroke();
  }
  if (e.stun > 0) {
    ctx.strokeStyle = '#a5f3fc'; ctx.lineWidth = 1.5; ctx.beginPath();
    for (let i = 0; i < 3; i++) { const sx = rand(b.l, b.r), sy = rand(b.t, b.b); ctx.moveTo(sx, sy); ctx.lineTo(sx + rand(-8, 8), sy + rand(-8, 8)); }
    ctx.stroke();
  }
  // health bar
  if (e.hp < e.maxHp && !e.d.boss) {
    const bw = Math.max(28, w * 0.8), by = b.t - 9;
    ctx.fillStyle = 'rgba(0,0,0,0.6)'; ctx.fillRect(x - bw / 2, by, bw, 4);
    ctx.fillStyle = e.armor > 0 ? '#e5e7eb' : '#ef4444'; ctx.fillRect(x - bw / 2, by, bw * Math.max(0, e.hp / e.maxHp), 4);
  }
}

// One painter per silhouette (R5). Front of every creature faces left.
const DRAW = {
  crab(ctx, e, b, c, step) {
    const cx = e.x, cy = b.b - 9;
    ctx.strokeStyle = rgb(c, 0.6); ctx.lineWidth = 2;
    for (let i = 0; i < 3; i++) { const o = i * 7 - 7; ctx.beginPath(); ctx.moveTo(cx + o, cy); ctx.lineTo(cx + o - 4 + step * 3, b.b); ctx.stroke(); }
    ctx.fillStyle = rgb(c); ctx.beginPath(); ctx.ellipse(cx, cy, 16, 8, 0, Math.PI, 0); ctx.fill(); ctx.fillRect(cx - 16, cy - 1, 32, 3);
    ctx.fillStyle = rgb(c, 1.15);
    ctx.beginPath(); ctx.arc(b.l + 2, cy - 4 + step * 2, 5, 0, Math.PI * 2); ctx.fill();
    ctx.strokeStyle = rgb(c, 0.8); ctx.beginPath(); ctx.moveTo(cx - 4, cy - 7); ctx.lineTo(cx - 5, b.t + 3); ctx.moveTo(cx + 3, cy - 7); ctx.lineTo(cx + 4, b.t + 3); ctx.stroke();
    ctx.fillStyle = '#fff'; ctx.beginPath(); ctx.arc(cx - 5, b.t + 3, 2.5, 0, Math.PI * 2); ctx.arc(cx + 4, b.t + 3, 2.5, 0, Math.PI * 2); ctx.fill();
    ctx.fillStyle = '#000'; ctx.fillRect(cx - 6, b.t + 2, 1.5, 1.5); ctx.fillRect(cx + 3, b.t + 2, 1.5, 1.5);
  },
  drowner(ctx, e, b, c, step) {
    const cx = e.x, hip = b.b - 22;
    ctx.strokeStyle = rgb(c, 0.6); ctx.lineWidth = 5; ctx.lineCap = 'round';
    ctx.beginPath(); ctx.moveTo(cx, hip); ctx.lineTo(cx - 6 + step * 6, b.b); ctx.moveTo(cx + 2, hip); ctx.lineTo(cx + 6 - step * 6, b.b); ctx.stroke();
    ctx.fillStyle = rgb(c); ctx.beginPath(); ctx.ellipse(cx + 2, hip - 12, 11, 16, -0.25, 0, Math.PI * 2); ctx.fill();
    const arm = e.attacking ? Math.sin(e.anim * 12) * 6 : step * 3;
    ctx.strokeStyle = rgb(c, 0.85); ctx.lineWidth = 4;
    ctx.beginPath(); ctx.moveTo(cx - 4, hip - 22); ctx.lineTo(b.l - 4 + arm, hip - 14); ctx.stroke();
    ctx.lineCap = 'butt';
    const hx = cx - 3, hy = b.t + 9;
    ctx.fillStyle = rgb(c, 1.1); ctx.beginPath(); ctx.arc(hx, hy, 9, 0, Math.PI * 2); ctx.fill();
    ctx.fillStyle = rgb(c, 0.7); ctx.beginPath(); ctx.moveTo(hx + 2, hy - 8); ctx.lineTo(hx + 12, hy - 14); ctx.lineTo(hx + 8, hy); ctx.fill();
    ctx.fillStyle = '#fde047'; ctx.fillRect(hx - 6, hy - 2, 3, 3); ctx.fillRect(hx - 1, hy - 2, 3, 3);
  },
  gull(ctx, e, b, c) {
    const cx = e.x, cy = (b.t + b.b) / 2, flap = Math.sin(e.anim * (e.state === 'hover' ? 30 : 14)) * 12;
    ctx.fillStyle = rgb(c);
    ctx.beginPath(); ctx.ellipse(cx, cy, 15, 6, e.state === 'dive' ? -0.5 : 0, 0, Math.PI * 2); ctx.fill();
    ctx.fillStyle = rgb(c, 0.85);
    ctx.beginPath(); ctx.moveTo(cx - 4, cy - 2); ctx.lineTo(cx + 8, cy - 4 - flap - 8); ctx.lineTo(cx + 12, cy - 2); ctx.fill();
    ctx.fillStyle = '#6b7280'; ctx.beginPath(); ctx.moveTo(cx + 10, cy - 1); ctx.lineTo(cx + 20, cy - 4); ctx.lineTo(cx + 18, cy + 3); ctx.fill();
    ctx.fillStyle = rgb(c, 1.05); ctx.beginPath(); ctx.arc(b.l + 5, cy - 2, 5, 0, Math.PI * 2); ctx.fill();
    ctx.fillStyle = '#f59e0b'; ctx.beginPath(); ctx.moveTo(b.l + 1, cy - 3); ctx.lineTo(b.l - 7, cy - 1); ctx.lineTo(b.l + 1, cy + 1); ctx.fill();
    ctx.fillStyle = '#000'; ctx.fillRect(b.l + 3, cy - 4, 2, 2);
  },
  puffer(ctx, e, b, c, step, t) {
    const swell = clamp((FIELD.wallFace + 260 - e.x) / 260, 0, 1);
    const r = 14 * (1 + 0.45 * swell), cx = e.x, cy = b.b - 17 - Math.abs(Math.sin(e.anim * 4)) * 6;
    const warn = swell > 0.5 && Math.sin(t * 20) > 0;
    const col = warn ? [239, 68, 68] : c;
    ctx.strokeStyle = rgb(col, 0.7); ctx.lineWidth = 2;
    for (let i = 0; i < 12; i++) { const an = i / 12 * Math.PI * 2; ctx.beginPath(); ctx.moveTo(cx + Math.cos(an) * r, cy + Math.sin(an) * r); ctx.lineTo(cx + Math.cos(an) * (r + 6), cy + Math.sin(an) * (r + 6)); ctx.stroke(); }
    ctx.fillStyle = rgb(col); ctx.beginPath(); ctx.arc(cx, cy, r, 0, Math.PI * 2); ctx.fill();
    ctx.fillStyle = rgb(col, 1.25); ctx.beginPath(); ctx.ellipse(cx, cy + r * 0.4, r * 0.7, r * 0.4, 0, 0, Math.PI * 2); ctx.fill();
    ctx.fillStyle = '#000'; ctx.beginPath(); ctx.arc(cx - r * 0.5, cy - r * 0.3, 2.5, 0, Math.PI * 2); ctx.fill();
  },
  eel(ctx, e, b, c) {
    const n = 9;
    for (let i = n - 1; i >= 0; i--) {
      const f = i / (n - 1), px = b.l + 6 + f * (e.w - 10), py = (b.t + b.b) / 2 + Math.sin(e.anim * 10 - f * 5) * 5;
      ctx.fillStyle = rgb(c, 1 - f * 0.3); ctx.beginPath(); ctx.arc(px, py, 7 - f * 4, 0, Math.PI * 2); ctx.fill();
    }
    const hy = (b.t + b.b) / 2 + Math.sin(e.anim * 10) * 5;
    ctx.fillStyle = '#a7f3d0'; ctx.beginPath(); ctx.arc(b.l + 4, hy - 2, 2.2, 0, Math.PI * 2); ctx.fill();
    ctx.fillStyle = '#5eead4'; ctx.fillRect(b.l + 8, b.t + 2, 30, 1.5);
  },
  shellback(ctx, e, b, c, step) {
    const cx = e.x + 6, base = b.b - 8;
    ctx.fillStyle = rgb(c, 0.7);
    for (let i = 0; i < 4; i++) ctx.fillRect(b.l + 12 + i * 13, base - 2 + (i % 2 ? step : -step) * 2, 8, 10);
    ctx.fillStyle = rgb(c); ctx.beginPath(); ctx.ellipse(cx, base, 30, 30, 0, Math.PI, 0); ctx.fill();
    ctx.strokeStyle = rgb(c, 0.6); ctx.lineWidth = 2;
    ctx.beginPath(); ctx.moveTo(cx - 14, base); ctx.lineTo(cx - 8, base - 22); ctx.lineTo(cx + 8, base - 22); ctx.lineTo(cx + 14, base); ctx.moveTo(cx - 8, base - 22); ctx.lineTo(cx, base - 30); ctx.lineTo(cx + 8, base - 22); ctx.stroke();
    ctx.fillStyle = rgb(c, 1.0); ctx.fillRect(cx - 30, base - 3, 60, 5);
    const hx = b.l + 7, hy = b.t + h0(e) * 0.62;
    ctx.fillStyle = rgb(c, 1.25); ctx.beginPath(); ctx.ellipse(hx, hy, 9, 7, 0, 0, Math.PI * 2); ctx.fill();
    ctx.fillStyle = '#fef08a'; ctx.fillRect(hx - 5, hy - 3, 3, 3);
  },
  siren(ctx, e, b, c, step, t) {
    const cx = e.x, hy = b.t + 9;
    if (e.singT > 0) {
      ctx.strokeStyle = `rgba(94,234,212,${e.singT * 0.6})`; ctx.lineWidth = 2;
      ctx.beginPath(); ctx.arc(cx, hy + 10, 30 + (1 - e.singT) * 30, 0, Math.PI * 2); ctx.stroke();
    }
    ctx.fillStyle = rgb(c, 0.8); ctx.beginPath(); ctx.moveTo(cx - 6, hy + 8); ctx.lineTo(cx - 16, b.b); ctx.lineTo(cx + 16, b.b); ctx.lineTo(cx + 6, hy + 8); ctx.fill();
    ctx.fillStyle = '#134e4a';
    ctx.beginPath(); ctx.moveTo(cx - 6, hy - 6); ctx.quadraticCurveTo(cx + 18 + Math.sin(t * 3) * 4, hy + 10, cx + 10, hy + 40); ctx.lineTo(cx + 2, hy + 4); ctx.fill();
    ctx.fillStyle = rgb(c, 1.15); ctx.beginPath(); ctx.arc(cx, hy, 8, 0, Math.PI * 2); ctx.fill();
    ctx.fillStyle = '#f0fdfa'; ctx.fillRect(cx - 5, hy - 2, 3, 2); ctx.fillRect(cx, hy - 2, 3, 2);
    ctx.strokeStyle = rgb(c); ctx.lineWidth = 3; ctx.beginPath(); ctx.moveTo(cx - 4, hy + 14); ctx.lineTo(cx - 14, hy + 4 + Math.sin(t * 4) * 4); ctx.stroke();
  },
  clawback(ctx, e, b, c, step) {
    const base = b.b - 12, cx = e.x + 10;
    ctx.strokeStyle = rgb(c, 0.6); ctx.lineWidth = 3;
    for (let i = 0; i < 4; i++) { const lx = cx - 20 + i * 12; ctx.beginPath(); ctx.moveTo(lx, base); ctx.lineTo(lx - 6 + step * 4 * (i % 2 ? 1 : -1), b.b); ctx.stroke(); }
    ctx.fillStyle = rgb(c);
    for (let i = 0; i < 4; i++) { ctx.beginPath(); ctx.ellipse(cx + 18 + i * 9, base - 12 + i * 2, 12 - i * 2, 12 - i * 2, 0, 0, Math.PI * 2); ctx.fill(); }
    ctx.beginPath(); ctx.ellipse(cx - 6, base - 16, 26, 18, 0, 0, Math.PI * 2); ctx.fill();
    const raise = e.swing > 0 ? -12 : 0;
    ctx.fillStyle = rgb(c, 1.1);
    ctx.beginPath(); ctx.ellipse(b.l + 12, base - 8 + raise, 14, 9, -0.4, 0, Math.PI * 2); ctx.fill();
    ctx.fillStyle = rgb(c, 0.8); ctx.beginPath(); ctx.moveTo(b.l + 2, base - 14 + raise); ctx.lineTo(b.l - 8, base - 22 + raise); ctx.lineTo(b.l + 6, base - 10 + raise); ctx.fill();
    ctx.strokeStyle = rgb(c, 0.9); ctx.lineWidth = 2;
    ctx.beginPath(); ctx.moveTo(b.l + 18, b.t + 14); ctx.lineTo(b.l + 10, b.t + 4); ctx.moveTo(b.l + 24, b.t + 14); ctx.lineTo(b.l + 22, b.t + 2); ctx.stroke();
    ctx.fillStyle = '#111'; ctx.beginPath(); ctx.arc(b.l + 10, b.t + 5, 3.5, 0, Math.PI * 2); ctx.arc(b.l + 22, b.t + 3, 3.5, 0, Math.PI * 2); ctx.fill();
    ctx.fillStyle = '#fde047'; ctx.fillRect(b.l + 9, b.t + 4, 1.5, 1.5); ctx.fillRect(b.l + 21, b.t + 2, 1.5, 1.5);
  },
  crabking(ctx, e, b, c, step) {
    const cx = e.x, cy = b.b - 28;
    ctx.strokeStyle = rgb(c, 0.6); ctx.lineWidth = 5;
    for (let i = 0; i < 4; i++) { const o = i * 22 - 33; ctx.beginPath(); ctx.moveTo(cx + o, cy); ctx.lineTo(cx + o - 10 + step * 8, b.b); ctx.stroke(); }
    ctx.fillStyle = rgb(c); ctx.beginPath(); ctx.ellipse(cx, cy, 70, 34, 0, Math.PI, 0); ctx.fill(); ctx.fillRect(cx - 70, cy - 2, 140, 8);
    const raise = e.swing > 0 ? -16 : 0;
    ctx.fillStyle = rgb(c, 1.15); ctx.beginPath(); ctx.ellipse(b.l + 8, cy - 14 + raise, 22, 15, -0.3, 0, Math.PI * 2); ctx.fill();
    ctx.fillStyle = rgb(c, 0.75); ctx.beginPath(); ctx.moveTo(b.l - 4, cy - 22 + raise); ctx.lineTo(b.l - 20, cy - 34 + raise); ctx.lineTo(b.l + 2, cy - 12 + raise); ctx.fill();
    ctx.strokeStyle = rgb(c, 0.8); ctx.lineWidth = 3; ctx.beginPath(); ctx.moveTo(cx - 12, cy - 30); ctx.lineTo(cx - 14, b.t + 26); ctx.moveTo(cx + 12, cy - 30); ctx.lineTo(cx + 14, b.t + 26); ctx.stroke();
    ctx.fillStyle = '#fff'; ctx.beginPath(); ctx.arc(cx - 14, b.t + 24, 6, 0, Math.PI * 2); ctx.arc(cx + 14, b.t + 24, 6, 0, Math.PI * 2); ctx.fill();
    ctx.fillStyle = '#000'; ctx.beginPath(); ctx.arc(cx - 16, b.t + 24, 2.5, 0, Math.PI * 2); ctx.arc(cx + 12, b.t + 24, 2.5, 0, Math.PI * 2); ctx.fill();
    ctx.fillStyle = '#facc15';
    ctx.beginPath(); ctx.moveTo(cx - 22, b.t + 20); ctx.lineTo(cx - 22, b.t + 4); ctx.lineTo(cx - 11, b.t + 12); ctx.lineTo(cx, b.t); ctx.lineTo(cx + 11, b.t + 12); ctx.lineTo(cx + 22, b.t + 4); ctx.lineTo(cx + 22, b.t + 20); ctx.fill();
    ctx.fillStyle = '#ef4444'; ctx.beginPath(); ctx.arc(cx, b.t + 14, 3, 0, Math.PI * 2); ctx.fill();
  },
  sirenqueen(ctx, e, b, c, step, t) {
    const cx = e.x, hy = b.t + 14;
    if (e.singT > 0) {
      ctx.strokeStyle = `rgba(94,234,212,${e.singT * 0.7})`; ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(cx, hy + 20, 50 + (1 - e.singT) * 60, 0, Math.PI * 2); ctx.stroke();
    }
    ctx.fillStyle = rgb(c, 0.75); ctx.beginPath(); ctx.moveTo(cx - 10, hy + 12); ctx.lineTo(cx - 30, b.b); ctx.lineTo(cx + 30, b.b); ctx.lineTo(cx + 10, hy + 12); ctx.fill();
    ctx.fillStyle = '#134e4a';
    ctx.beginPath(); ctx.moveTo(cx - 10, hy - 8); ctx.quadraticCurveTo(cx + 34 + Math.sin(t * 2) * 8, hy + 20, cx + 22, hy + 80); ctx.lineTo(cx + 4, hy + 6); ctx.fill();
    ctx.fillStyle = rgb(c, 1.15); ctx.beginPath(); ctx.arc(cx, hy, 13, 0, Math.PI * 2); ctx.fill();
    ctx.fillStyle = '#f0fdfa'; ctx.fillRect(cx - 8, hy - 3, 4, 3); ctx.fillRect(cx, hy - 3, 4, 3);
    ctx.fillStyle = '#fde68a'; ctx.beginPath(); ctx.moveTo(cx - 12, hy - 10); ctx.lineTo(cx - 10, hy - 22); ctx.lineTo(cx - 3, hy - 14); ctx.lineTo(cx, hy - 26); ctx.lineTo(cx + 3, hy - 14); ctx.lineTo(cx + 10, hy - 22); ctx.lineTo(cx + 12, hy - 10); ctx.fill();
    ctx.strokeStyle = '#fbbf24'; ctx.lineWidth = 3; ctx.beginPath(); ctx.moveTo(cx - 24, b.b); ctx.lineTo(cx - 28, b.t - 10); ctx.stroke();
    ctx.beginPath(); ctx.moveTo(cx - 36, b.t - 4); ctx.lineTo(cx - 36, b.t - 18); ctx.moveTo(cx - 28, b.t - 10); ctx.lineTo(cx - 28, b.t - 24); ctx.moveTo(cx - 20, b.t - 4); ctx.lineTo(cx - 20, b.t - 18); ctx.moveTo(cx - 36, b.t - 6); ctx.lineTo(cx - 20, b.t - 6); ctx.stroke();
  },
  leviathan(ctx, e, b, c, step, t) {
    const bob = Math.sin(t * 1.5) * 4;
    ctx.fillStyle = rgb(c, 0.8);
    ctx.beginPath(); ctx.moveTo(b.l + 30, b.b); ctx.quadraticCurveTo(b.l + 10, b.t + 40 + bob, b.l + 60, b.t + 10 + bob); ctx.quadraticCurveTo(b.r - 20, b.t - 10, b.r, b.b); ctx.fill();
    ctx.fillStyle = rgb(c, 1.1);
    for (let i = 0; i < 5; i++) { const fx = b.l + 80 + i * 28; ctx.beginPath(); ctx.moveTo(fx, b.t + 20 + i * 6 + bob); ctx.lineTo(fx + 14, b.t - 10 + i * 8 + bob); ctx.lineTo(fx + 24, b.t + 26 + i * 6 + bob); ctx.fill(); }
    ctx.fillStyle = rgb(c, 0.6);
    const jaw = e.swing > 0 ? 18 : 6;
    ctx.beginPath(); ctx.moveTo(b.l, b.t + 70 + bob); ctx.lineTo(b.l + 70, b.t + 60 + bob); ctx.lineTo(b.l + 60, b.t + 80 + jaw + bob); ctx.fill();
    ctx.fillStyle = '#e5e7eb';
    for (let i = 0; i < 5; i++) { ctx.beginPath(); ctx.moveTo(b.l + 8 + i * 11, b.t + 70 + bob); ctx.lineTo(b.l + 12 + i * 11, b.t + 80 + bob); ctx.lineTo(b.l + 16 + i * 11, b.t + 69 + bob); ctx.fill(); }
    const g = ctx.createRadialGradient(b.l + 30, b.t + 40 + bob, 2, b.l + 30, b.t + 40 + bob, 22);
    g.addColorStop(0, '#fef08a'); g.addColorStop(0.4, '#f59e0b'); g.addColorStop(1, 'rgba(245,158,11,0)');
    ctx.fillStyle = g; ctx.beginPath(); ctx.arc(b.l + 30, b.t + 40 + bob, 22, 0, Math.PI * 2); ctx.fill();
    ctx.fillStyle = '#111'; ctx.fillRect(b.l + 28, b.t + 32 + bob, 4, 16);
  },
};
const h0 = e => e.h;

function drawShots(ctx) {
  const N = Game.N;
  ctx.lineCap = 'round';
  for (const b of N.bullets) {
    ctx.strokeStyle = b.s.def.color; ctx.lineWidth = b.thick;
    ctx.beginPath(); ctx.moveTo(b.x - b.vx * 0.012, b.y - b.vy * 0.012); ctx.lineTo(b.x, b.y); ctx.stroke();
  }
  ctx.lineCap = 'butt';
  for (const s of N.shells) {
    ctx.fillStyle = s.gadget === 'frost' ? '#bae6fd' : '#3f3f46';
    ctx.beginPath(); ctx.arc(s.x, s.y, 5, 0, Math.PI * 2); ctx.fill();
  }
  for (const f of N.flames) {
    const p = 1 - f.life / f.max;
    ctx.fillStyle = p < 0.3 ? 'rgba(254,240,138,0.9)' : p < 0.7 ? 'rgba(251,146,60,0.75)' : 'rgba(127,29,29,0.4)';
    ctx.beginPath(); ctx.arc(f.x, f.y, 4 + p * 12, 0, Math.PI * 2); ctx.fill();
  }
  for (const bo of N.bolts) {
    ctx.strokeStyle = bo.color; ctx.lineWidth = bo.width; ctx.globalAlpha = Math.max(0, bo.life / bo.max);
    ctx.beginPath(); ctx.moveTo(...bo.pts[0]); bo.pts.slice(1).forEach(p => ctx.lineTo(...p)); ctx.stroke();
  }
  ctx.globalAlpha = 1;
}

function drawParts(ctx) {
  for (const p of Game.N.parts) {
    const a = Math.max(0, p.life / p.max);
    ctx.globalAlpha = p.kind === 'smoke' ? a * 0.7 : a;
    ctx.fillStyle = p.color;
    if (p.kind === 'spark') { ctx.strokeStyle = p.color; ctx.lineWidth = p.size * 0.6; ctx.beginPath(); ctx.moveTo(p.x, p.y); ctx.lineTo(p.x - p.vx * 0.03, p.y - p.vy * 0.03); ctx.stroke(); }
    else { const s = p.kind === 'smoke' ? p.size * (2 - a) : p.size; ctx.beginPath(); ctx.arc(p.x, p.y, s, 0, Math.PI * 2); ctx.fill(); }
  }
  ctx.globalAlpha = 1;
}

function drawFog(ctx, t) {
  const g = ctx.createLinearGradient(620, 0, 980, 0);
  g.addColorStop(0, 'rgba(71,85,105,0)'); g.addColorStop(1, 'rgba(71,85,105,0.9)');
  ctx.save();
  ctx.fillStyle = g; ctx.fillRect(620, 230, W - 620, H - 230);
  ctx.fillStyle = 'rgba(148,163,184,0.1)';
  for (let i = 0; i < 6; i++) { ctx.beginPath(); ctx.ellipse(700 + ((i * 160 + t * 20) % 700), 430 + i * 28, 150, 36, 0, 0, Math.PI * 2); ctx.fill(); }
  ctx.restore();
  // eyes glint through the fog so the player can still track threats
  for (const e of Game.N.enemies) {
    if (e.x < 760 || e.d.flying) continue;
    const b = boxOf(e), hd = e.d.head || [0.1, 0.1, 0.4, 0.4];
    const ex = b.l + (hd[0] + hd[2]) / 2 * e.w, ey = b.t + (hd[1] + hd[3]) / 2 * e.h;
    ctx.fillStyle = `rgba(253,224,71,${0.55 + 0.25 * Math.sin(t * 3 + e.anim)})`;
    ctx.fillRect(ex - 4, ey - 1, 3, 2); ctx.fillRect(ex + 1, ey - 1, 3, 2);
  }
}

function drawMark(ctx, e, t) {
  const c = centerOf(e), r = Math.max(e.w, e.h) * 0.6 + 4 + Math.sin(t * 6) * 2;
  ctx.strokeStyle = 'rgba(248,113,113,0.9)'; ctx.lineWidth = 1.5;
  ctx.beginPath(); ctx.arc(c.x, c.y, r, 0, Math.PI * 2); ctx.stroke();
  ctx.beginPath(); ctx.moveTo(c.x - r - 5, c.y); ctx.lineTo(c.x - r + 5, c.y); ctx.moveTo(c.x + r - 5, c.y); ctx.lineTo(c.x + r + 5, c.y); ctx.stroke();
}

function drawTexts(ctx) {
  ctx.textAlign = 'center';
  for (const t of Game.N.texts) {
    ctx.globalAlpha = Math.min(1, t.life / t.max * 2);
    ctx.font = `${t.bold ? '800' : '700'} ${t.size}px "Trebuchet MS", sans-serif`;
    ctx.lineWidth = 3; ctx.strokeStyle = 'rgba(0,0,0,0.7)'; ctx.strokeText(t.str, t.x, t.y);
    ctx.fillStyle = t.color; ctx.fillText(t.str, t.x, t.y);
  }
  ctx.globalAlpha = 1;
}

function roundRect(ctx, x, y, w, h, r) { ctx.beginPath(); ctx.roundRect ? ctx.roundRect(x, y, w, h, r) : ctx.rect(x, y, w, h); }

function drawHud(ctx, t) {
  const N = Game.N, run = Game.run;
  ctx.font = '700 13px "Trebuchet MS", sans-serif'; ctx.textAlign = 'left';
  // wall
  const mx = wallMax(run), pct = run.wallHp / mx;
  ctx.fillStyle = 'rgba(0,0,0,0.55)'; roundRect(ctx, 16, 14, 280, 34, 6); ctx.fill();
  ctx.fillStyle = '#27272a'; ctx.fillRect(26, 32, 260, 9);
  ctx.fillStyle = pct > 0.5 ? '#4ade80' : pct > 0.25 ? '#facc15' : '#ef4444';
  if (pct <= 0.25 && Math.sin(t * 10) > 0) ctx.fillStyle = '#fca5a5';
  ctx.fillRect(26, 32, 260 * pct, 9);
  ctx.fillStyle = '#e5e7eb'; ctx.fillText(`WALL  ${Math.ceil(run.wallHp)} / ${mx}`, 26, 28);
  if (run.fort.plating) { ctx.fillStyle = '#a1a1aa'; ctx.textAlign = 'right'; ctx.fillText(`⛨${run.fort.plating}`, 286, 28); ctx.textAlign = 'left'; }
  // night / progress
  ctx.textAlign = 'center';
  ctx.fillStyle = 'rgba(0,0,0,0.55)'; roundRect(ctx, W / 2 - 150, 14, 300, 34, 6); ctx.fill();
  ctx.fillStyle = '#e5e7eb'; ctx.fillText(`NIGHT ${N.n}  ·  ${N.card.icon} ${N.card.name}`, W / 2, 28);
  const prog = N.queue.length ? N.qi / N.queue.length : 1;
  ctx.fillStyle = '#27272a'; ctx.fillRect(W / 2 - 140, 34, 280, 6);
  ctx.fillStyle = '#60a5fa'; ctx.fillRect(W / 2 - 140, 34, 280 * prog, 6);
  // scrap
  ctx.fillStyle = 'rgba(0,0,0,0.55)'; roundRect(ctx, W - 216, 14, 200, 34, 6); ctx.fill();
  ctx.textAlign = 'right'; ctx.fillStyle = '#fbbf24';
  ctx.fillText(`⚙ ${run.scrap}   +${Math.round(N.earned.bounty + N.earned.pearls)} tonight`, W - 28, 36);
  // boss bar
  if (N.boss && !N.boss.dead) {
    const e = N.boss;
    ctx.textAlign = 'center';
    ctx.fillStyle = 'rgba(0,0,0,0.6)'; ctx.fillRect(W / 2 - 260, 56, 520, 22);
    ctx.fillStyle = '#7f1d1d'; ctx.fillRect(W / 2 - 254, 68, 508, 6);
    ctx.fillStyle = '#ef4444'; ctx.fillRect(W / 2 - 254, 68, 508 * Math.max(0, e.hp / e.maxHp), 6);
    ctx.fillStyle = '#fecaca'; ctx.font = '700 11px "Trebuchet MS", sans-serif'; ctx.fillText(e.d.name.toUpperCase(), W / 2, 65);
  }
  // weapons
  ctx.textAlign = 'left';
  N.arms.forEach((a, i) => {
    const x = 16 + i * 176, y = H - 70, act = i === N.cur;
    ctx.fillStyle = act ? 'rgba(30,58,138,0.85)' : 'rgba(0,0,0,0.55)'; roundRect(ctx, x, y, 168, 54, 6); ctx.fill();
    if (act) { ctx.strokeStyle = '#93c5fd'; ctx.lineWidth = 1.5; roundRect(ctx, x, y, 168, 54, 6); ctx.stroke(); }
    ctx.font = '700 12px "Trebuchet MS", sans-serif'; ctx.fillStyle = '#e5e7eb';
    ctx.fillText(`${i + 1}  ${a.s.def.name}`, x + 8, y + 16);
    const own = run.weapons[a.id];
    ctx.font = '10px "Trebuchet MS", sans-serif'; ctx.fillStyle = '#a5b4fc';
    ctx.fillText(own.mods.filter(Boolean).map(m => MODS[m].name).join(' · ') || `Tier ${own.tier}`, x + 8, y + 30);
    if (a.reloading) {
      ctx.fillStyle = '#27272a'; ctx.fillRect(x + 8, y + 38, 152, 8);
      ctx.fillStyle = a.jam ? '#ef4444' : '#93c5fd'; ctx.fillRect(x + 8, y + 38, 152 * Math.min(1, a.rt / a.rTotal), 8);
      if (i !== N.cur) { ctx.fillStyle = '#38bdf8'; ctx.font = '9px sans-serif'; ctx.fillText('DECKHAND', x + 110, y + 30); }
    } else {
      ctx.font = '700 13px "Trebuchet MS", sans-serif';
      ctx.fillStyle = a.ammo / a.s.mag < 0.25 ? '#f87171' : a.perfect ? '#86efac' : '#e5e7eb';
      ctx.fillText(N.overcharge > 0 && act ? '∞' : `${a.ammo} / ${a.s.mag}${a.perfect ? '  PERFECT' : ''}`, x + 8, y + 47);
    }
  });
  // gadgets
  N.gadgets.forEach((g, i) => {
    if (!g) return;
    const x = W - 16 - (N.gadgets.length - i) * 128, y = H - 70;
    ctx.fillStyle = 'rgba(0,0,0,0.55)'; roundRect(ctx, x, y, 120, 54, 6); ctx.fill();
    const def = GADGETS[g.id];
    if (g.cd > 0) { ctx.fillStyle = 'rgba(59,130,246,0.25)'; ctx.fillRect(x, y + 54 * (1 - g.cd / def.cd), 120, 54 * (g.cd / def.cd)); }
    ctx.fillStyle = g.cd > 0 ? '#9ca3af' : '#fde68a'; ctx.font = '700 12px "Trebuchet MS", sans-serif';
    ctx.fillText(`[${i === 0 ? 'SPACE' : 'E'}]`, x + 8, y + 18);
    ctx.fillStyle = '#e5e7eb'; ctx.fillText(def.name, x + 8, y + 36);
    if (g.cd > 0) { ctx.fillStyle = '#9ca3af'; ctx.fillText(g.cd.toFixed(1) + 's', x + 8, y + 50); }
    else { ctx.fillStyle = '#86efac'; ctx.fillText('READY', x + 8, y + 50); }
  });
  ctx.font = '10px "Trebuchet MS", sans-serif'; ctx.fillStyle = 'rgba(229,231,235,0.5)'; ctx.textAlign = 'center';
  ctx.fillText('R reload (tap again in the bright zone)   ·   1/2/3 / Q / wheel swap   ·   Esc pause   ·   M mute', W / 2, H - 6);
  // banner
  if (N.bannerT > 0) {
    ctx.globalAlpha = Math.min(1, N.bannerT);
    ctx.font = '800 44px "Trebuchet MS", sans-serif'; ctx.textAlign = 'center';
    ctx.lineWidth = 5; ctx.strokeStyle = 'rgba(0,0,0,0.6)';
    const s = N.bannerBoss ? N.bannerBoss.toUpperCase() + ' RISES' : `NIGHT ${N.n}`;
    ctx.strokeText(s, W / 2, 200); ctx.fillStyle = N.bannerBoss ? '#fca5a5' : '#e0e7ff'; ctx.fillText(s, W / 2, 200);
    ctx.font = '600 16px "Trebuchet MS", sans-serif'; ctx.fillStyle = '#cbd5e1';
    ctx.fillText(N.bannerBoss ? '' : `${N.card.icon} ${N.card.name} — ${N.card.desc}`, W / 2, 228);
    ctx.globalAlpha = 1;
  }
  if (N.over) {
    ctx.fillStyle = `rgba(127,29,29,${Math.min(0.5, N.overT * 0.4)})`; ctx.fillRect(0, 0, W, H);
    ctx.font = '800 54px "Trebuchet MS", sans-serif'; ctx.textAlign = 'center'; ctx.fillStyle = '#fecaca';
    ctx.fillText('THE WALL HAS FALLEN', W / 2, H / 2);
  }
}

function drawCrosshair(ctx) {
  const { mx, my } = Game.input, N = Game.N;
  const a = N && N.arms[N.cur];
  const sp = a ? 8 + a.s.spread * 120 : 8;
  ctx.strokeStyle = 'rgba(255,255,255,0.9)'; ctx.lineWidth = 1.5;
  ctx.beginPath(); ctx.arc(mx, my, sp, 0, Math.PI * 2); ctx.stroke();
  ctx.beginPath(); ctx.moveTo(mx - sp - 6, my); ctx.lineTo(mx - sp + 2, my); ctx.moveTo(mx + sp - 2, my); ctx.lineTo(mx + sp + 6, my);
  ctx.moveTo(mx, my - sp - 6); ctx.lineTo(mx, my - sp + 2); ctx.moveTo(mx, my + sp - 2); ctx.lineTo(mx, my + sp + 6); ctx.stroke();
  ctx.fillStyle = '#fff'; ctx.fillRect(mx - 1, my - 1, 2, 2);
  if (a && a.s.kind === 'lob') {
    ctx.strokeStyle = 'rgba(252,165,165,0.6)'; ctx.setLineDash([3, 3]);
    ctx.beginPath(); ctx.arc(mx, Math.min(my, FIELD.groundY), a.s.splash, 0, Math.PI * 2); ctx.stroke(); ctx.setLineDash([]);
  }
  if (a && a.reloading && N.cur === N.arms.indexOf(a)) {
    const w = 90, x = mx - w / 2, y = my + 22, z = BALANCE.perfectZone;
    ctx.fillStyle = 'rgba(0,0,0,0.65)'; ctx.fillRect(x - 2, y - 2, w + 4, 12);
    if (!a.tried) { ctx.fillStyle = 'rgba(134,239,172,0.85)'; ctx.fillRect(x + w * z[0], y, w * (z[1] - z[0]), 8); }
    ctx.fillStyle = a.jam ? '#ef4444' : '#e5e7eb';
    ctx.fillRect(x + w * Math.min(1, a.rt / a.rTotal) - 1.5, y - 3, 3, 14);
  }
}
