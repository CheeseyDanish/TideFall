// Tidewall — headless balance harness. Loaded only with ?dev in the URL (docs/TECH.md).
// Usage in the console:  DevSim.report()   or   DevSim.night(10, DevSim.PLANS[10], 'storm')
'use strict';

const DevSim = (() => {
  // "Reasonable player" loadouts per night: [weapons, mods per weapon, gadgets, traps, crew]
  const PLANS = {
    1: [['pistol'], [[]], [null, null], [], []],
    3: [['pistol', 'scattergun'], [[], []], [null, null], ['spikes'], []],
    5: [['scattergun', 'pistol'], [['voltaic'], []], ['frost', null], ['spikes', 'oil'], []],
    7: [['scattergun', 'smg'], [['voltaic'], ['cryo']], ['frost', null], ['spikes', 'sprinkler', 'post'], ['rifleman']],
    10: [['longrifle', 'scattergun', 'smg'], [['hollow'], ['voltaic'], ['cryo']], ['storm', null], ['spikes', 'sprinkler', 'post'], ['rifleman', 'mechanic']],
    13: [['longrifle', 'mortar', 'arc'], [['hollow', 'piercing'], ['cryo'], ['voltaic']], ['storm', 'depth'], ['oil', 'sprinkler', 'post', 'mine'], ['rifleman', 'mechanic', 'spotter']],
    16: [['longrifle', 'mortar', 'arc'], [['hollow', 'piercing'], ['cryo', 'blast'], ['voltaic', 'hair']], ['storm', 'depth'], ['oil', 'sprinkler', 'post', 'mine', 'spikes'], ['rifleman', 'mechanic', 'spotter', 'deckhand']],
    20: [['longrifle', 'mortar', 'arc'], [['hollow', 'piercing'], ['cryo', 'blast'], ['voltaic', 'hair']], ['storm', 'depth'], ['oil', 'sprinkler', 'post', 'mine', 'spikes'], ['rifleman', 'mechanic', 'spotter', 'deckhand']],
  };

  function setup(n, [weapons, mods, gadgets, traps, crew]) {
    const r = newRun();
    r.night = n;
    r.slots = { weapon: weapons.length, gadget: Math.max(1, gadgets.filter(Boolean).length), crew: 4, trap: 5 };
    const tier = Math.max(1, Math.min(5, Math.round(n / 4)));
    weapons.forEach((w, i) => { const m = (mods[i] || []).slice(); while (m.length < 2) m.push(null); r.weapons[w] = { tier, sockets: 2, mods: m, spent: 0 }; });
    r.loadout.weapons = weapons.concat([null, null, null]).slice(0, 3);
    r.loadout.gadgets = gadgets.slice();
    gadgets.forEach(g => { if (g) r.gadgets[g] = true; });
    r.placed = traps.map((t, i) => ({ id: t, x: 300 + i * 170 }));
    traps.forEach(t => { r.traps[t] = (r.traps[t] || 0) + 1; });
    r.crew = crew.map(id => ({ id, lvl: 1 + Math.floor(n / 10), spent: 0 }));
    r.fort.reinforce = Math.floor(n / 3); r.fort.plating = Math.floor(n / 6);
    r.wallHp = wallMax(r);
    Game.run = r;
    return r;
  }

  // Bot: retargets the enemy nearest the wall every 0.5 s, aims at the weak point with ±jitter px,
  // fires `duty` of the time, swaps weapons every 4 s, uses gadgets on cooldown.
  function play(card, secs = 400, opts = {}) {
    const set = Store.set, del = Store.del;
    Store.set = Store.del = () => {};          // never touch the player's real save
    try { return playInner(card, secs, opts); } finally { Store.set = set; Store.del = del; }
  }
  function playInner(card, secs, { jitter = 24, duty = 0.7 } = {}) {
    startNight(card);
    const G = Game, hp0 = G.run.wallHp;
    let t = 0, f = 0, tg = null, on = true, minX = 9999, err = null;
    try {
      while (t < secs && G.N) {
        const N = G.N;
        if (f % 30 === 0) {
          tg = null;
          for (const e of N.enemies) if (!e.dead && e.emerge <= 0 && e.x < 1250 && (!tg || e.x < tg.x)) tg = e;
          // players focus the boss unless something is already chewing near the wall
          if (N.boss && !N.boss.dead && N.boss.emerge <= 0 && (!tg || tg.x > FIELD.wallFace + 250)) tg = N.boss;
        }
        if (f % 45 === 0) on = Math.random() < duty;
        if (tg && !tg.dead) {
          const b = boxOf(tg), hd = tg.d.head || [0.3, 0.3, 0.7, 0.7];
          G.input.mx = b.l + (hd[0] + hd[2]) / 2 * (b.r - b.l) + (Math.random() - 0.5) * jitter;
          G.input.my = b.t + (hd[1] + hd[3]) / 2 * (b.b - b.t) + (Math.random() - 0.5) * jitter;
          G.input.down = on;
        } else G.input.down = false;
        for (const e of N.enemies) if (!e.d.flying) minX = Math.min(minX, e.x);
        if (f % 240 === 0 && N.arms.length > 1) switchTo((N.cur + 1) % N.arms.length);
        if (f % 120 === 0) { useGadget(0); useGadget(1); }
        updateNight(1 / 60); t += 1 / 60; f++;
      }
    } catch (e) { err = e.stack; }
    G.input.down = false;
    const lost = G.state === 'gameover' ? hp0 : hp0 - G.run.wallHp;
    return { state: G.state, lostPct: Math.round(100 * lost / hp0), minX: Math.round(minX), secs: Math.round(t), err };
  }

  function night(n, plan = PLANS[n], card = 'calm', opts) { setup(n, plan); return play(card, 400, opts); }

  function report(runs = 3, opts) {
    const rows = [];
    for (const n of Object.keys(PLANS).map(Number)) {
      const r = [];
      for (let i = 0; i < runs; i++) r.push(night(n, PLANS[n], 'calm', opts));
      const lost = r.map(x => x.lostPct);
      rows.push(`night ${String(n).padStart(2)}: wall lost ${lost.join('/')}%  falls ${r.filter(x => x.state === 'gameover').length}/${runs}  closest ${Math.min(...r.map(x => x.minX))}px${r.some(x => x.err) ? '  ERROR ' + r.find(x => x.err).err : ''}`);
    }
    UI.showTitle();
    return rows.join('\n');
  }

  // Full campaign: real economy. Between nights the shopper repairs the wall, then buys down a priority
  // list (skipping what it can't afford yet). Shows whether income keeps pace with the difficulty curve.
  const SHOP_ORDER = [
    ['trap', 'spikes'], ['weapon', 'scattergun'], ['mod', 'voltaic', 'scattergun'], ['gadget', 'frost'],
    ['trap', 'sprinkler'], ['weapon', 'smg'], ['mod', 'cryo', 'smg'], ['fort', 'reinforce'], ['crew', 'rifleman'],
    ['tier', 'scattergun'], ['trap', 'post'], ['fort', 'reinforce'], ['tier', 'smg'], ['slot', 'weapon'], ['weapon', 'longrifle'],
    ['mod', 'hollow', 'longrifle'], ['crew', 'mechanic'], ['fort', 'plating'], ['tier', 'longrifle'], ['slot', 'gadget'],
    ['gadget', 'storm'], ['tier', 'scattergun'], ['socket', 'longrifle'], ['mod', 'piercing', 'longrifle'], ['fort', 'reinforce'],
    ['tier', 'longrifle'], ['tier', 'smg'], ['fort', 'plating'], ['slot', 'trap'], ['trap', 'oil'], ['fort', 'reinforce'],
    ['tier', 'scattergun'], ['tier', 'longrifle'], ['slot', 'crew'], ['crew', 'spotter'], ['fort', 'plating'], ['tier', 'smg'],
    ['fort', 'reinforce'], ['tier', 'longrifle'], ['tier', 'scattergun'], ['tier', 'smg'], ['fort', 'reinforce'], ['fort', 'reinforce'],
  ];
  function shop(r, done) {
    const pay = c => { if (r.scrap < c) return false; r.scrap -= c; return true; };
    const miss = Math.ceil(wallMax(r) - r.wallHp), rc = Math.ceil(miss * BALANCE.repairCostPerHp);
    if (miss > 0) { const paid = Math.min(rc, r.scrap); r.scrap -= paid; r.wallHp = Math.min(wallMax(r), r.wallHp + paid / BALANCE.repairCostPerHp); }
    for (let i = 0; i < SHOP_ORDER.length; i++) {
      if (done.has(i)) continue;
      const [k, id, w] = SHOP_ORDER[i];
      let ok = false;
      if (k === 'trap' && r.placed.length < r.slots.trap && pay(TRAPS[id].price)) { r.traps[id] = (r.traps[id] || 0) + 1; r.placed.push({ id, x: id === 'sprinkler' ? 330 : id === 'oil' ? 260 : id === 'post' ? 420 : 520 + r.placed.length * 90 }); ok = true; }
      else if (k === 'weapon' && pay(WEAPONS[id].price)) { r.weapons[id] = { tier: 1, sockets: 1, mods: [null], spent: 0 }; const f = r.loadout.weapons.slice(0, r.slots.weapon).indexOf(null); if (f >= 0) r.loadout.weapons[f] = id; else r.loadout.weapons[r.slots.weapon - 1] = id; ok = true; }
      else if (k === 'mod' && r.weapons[w] && r.weapons[w].mods.includes(null) && pay(MODS[id].price)) { r.mods[id] = (r.mods[id] || 0) + 1; r.weapons[w].mods[r.weapons[w].mods.indexOf(null)] = id; ok = true; }
      else if (k === 'gadget' && pay(GADGETS[id].price)) { r.gadgets[id] = true; const f = r.loadout.gadgets.slice(0, r.slots.gadget).indexOf(null); if (f >= 0) r.loadout.gadgets[f] = id; ok = true; }
      else if (k === 'tier' && r.weapons[id] && r.weapons[id].tier < 5 && pay(BALANCE.tierCost(WEAPONS[id].price, r.weapons[id].tier))) { r.weapons[id].tier++; ok = true; }
      else if (k === 'socket' && r.weapons[id] && pay(BALANCE.socketCost)) { r.weapons[id].sockets = 2; r.weapons[id].mods.push(null); ok = true; }
      else if (k === 'fort' && r.fort[id] < FORTIFY[id].max && pay(FORTIFY[id].cost(r.fort[id] + 1))) { r.fort[id]++; if (id === 'reinforce') r.wallHp += BALANCE.wallHpPerLevel; ok = true; }
      else if (k === 'slot' && pay(SLOTS[id].cost[r.slots[id] - SLOTS[id].start])) { r.slots[id]++; ok = true; }
      else if (k === 'crew' && r.crew.length < r.slots.crew && pay(CREW[id].price)) { r.crew.push({ id, lvl: 1, spent: 0 }); ok = true; }
      if (ok) done.add(i); else break;   // buy in order, like a player saving up
    }
  }
  function campaign(nights = 20, opts) {
    const r = newRun(), done = new Set(), rows = [];
    Game.run = r;
    for (let n = 1; n <= nights; n++) {
      shop(r, done);
      const hp = r.wallHp, s0 = r.scrap;
      const o = play('calm', 400, opts);
      if (o.err) { rows.push(`night ${n}: ERROR ${o.err}`); break; }
      rows.push(`night ${String(n).padStart(2)}: ${o.state === 'gameover' ? 'FELL' : 'held'}  wall ${Math.round(hp)}→${o.state === 'gameover' ? 0 : Math.round(r.wallHp)}/${wallMax(r)}  +${r.scrap - s0} scrap  bought ${done.size}/${SHOP_ORDER.length}`);
      if (o.state === 'gameover') break;
      Game.run = r;
    }
    UI.showTitle();
    return rows.join('\n');
  }

  return { PLANS, setup, play, night, report, campaign, SHOP_ORDER };
})();
