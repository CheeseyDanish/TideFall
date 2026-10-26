// Tidewall — DOM screens: title, shop, tide cards, summaries.
'use strict';

const UI = (() => {
  let root, tab = 'armory', offer = null, lastSummary = null;
  const $ = html => { root.innerHTML = html; root.classList.add('show'); };
  const hide = () => { root.innerHTML = ''; root.classList.remove('show'); };
  const esc = s => String(s).replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
  const btn = (act, label, data = {}, cls = '', disabled = false) =>
    `<button class="btn ${cls}" data-act="${act}" ${Object.entries(data).map(([k, v]) => `data-${k}="${esc(v)}"`).join(' ')} ${disabled ? 'disabled' : ''}>${label}</button>`;
  const cost = n => `⚙ ${n}`;

  // ---- shop actions (all purchases go through spend) -------------------------
  function spend(n) {
    const run = Game.run;
    if (run.scrap < n) { Sfx.play('deny'); flash('Not enough scrap'); return false; }
    run.scrap -= n; Sfx.play('buy'); return true;
  }
  function flash(msg) {
    const el = document.getElementById('toast');
    if (!el) return;
    el.textContent = msg; el.classList.remove('on'); void el.offsetWidth; el.classList.add('on');
  }
  const refund = n => Math.round(n * BALANCE.sellRefund);

  const A = {
    new() { Game.run = newRun(); tab = 'armory'; showHowTo(true); },
    continue() { Game.run = loadRun(); tab = 'armory'; showShop(); },
    howto() { showHowTo(false); },
    title() { showTitle(); },
    closeHow(d) { if (d.start === '1') showShop(); else showTitle(); },
    tab(d) { tab = d.tab; showShop(); },
    buyW(d) { const def = WEAPONS[d.id]; if (spend(def.price)) { Game.run.weapons[d.id] = { tier: 1, sockets: 1, mods: [null], spent: 0 }; autoEquipW(d.id); } },
    tierW(d) { const w = Game.run.weapons[d.id], c = BALANCE.tierCost(WEAPONS[d.id].price, w.tier); if (w.tier < BALANCE.tierMax && spend(c)) { w.tier++; w.spent += c; } },
    socket(d) { const w = Game.run.weapons[d.id]; if (w.sockets < 2 && spend(BALANCE.socketCost)) { w.sockets = 2; w.mods.push(null); w.spent += BALANCE.socketCost; } },
    sellW(d) {
      const run = Game.run, w = run.weapons[d.id];
      if (Object.keys(run.weapons).length <= 1) { flash('You need at least one weapon'); Sfx.play('deny'); return; }
      run.scrap += refund(WEAPONS[d.id].price + w.spent);
      delete run.weapons[d.id];
      run.loadout.weapons = run.loadout.weapons.map(x => x === d.id ? null : x);
      if (!run.loadout.weapons.some(Boolean)) autoEquipW(Object.keys(run.weapons)[0]);
      Sfx.play('coin');
    },
    equipW(d) {
      const L = Game.run.loadout.weapons, i = +d.slot, from = L.indexOf(d.id);
      if (from === i) { L[i] = null; } else { if (from >= 0) L[from] = L[i]; L[i] = d.id; }
      Sfx.play('click');
    },
    buyMod(d) { if (spend(MODS[d.id].price)) Game.run.mods[d.id] = (Game.run.mods[d.id] || 0) + 1; },
    sellMod(d) { const run = Game.run; if (modsFree(run, d.id) > 0) { run.mods[d.id]--; run.scrap += refund(MODS[d.id].price); Sfx.play('coin'); } },
    buyG(d) { if (spend(GADGETS[d.id].price)) { Game.run.gadgets[d.id] = true; const L = Game.run.loadout.gadgets; const free = L.slice(0, Game.run.slots.gadget).indexOf(null); if (free >= 0) L[free] = d.id; } },
    equipG(d) {
      const L = Game.run.loadout.gadgets, i = +d.slot, from = L.indexOf(d.id);
      if (from === i) L[i] = null; else { if (from >= 0) L[from] = L[i]; L[i] = d.id; }
      Sfx.play('click');
    },
    sellG(d) { const run = Game.run; delete run.gadgets[d.id]; run.loadout.gadgets = run.loadout.gadgets.map(x => x === d.id ? null : x); run.scrap += refund(GADGETS[d.id].price); Sfx.play('coin'); },
    repair(d) {
      const run = Game.run, missing = wallMax(run) - run.wallHp;
      const hp = d.amt === 'all' ? missing : Math.min(missing, +d.amt);
      const c = Math.ceil(hp * BALANCE.repairCostPerHp);
      if (hp > 0 && spend(c)) run.wallHp += hp;
    },
    fort(d) {
      const run = Game.run, f = FORTIFY[d.id], l = run.fort[d.id];
      if (l < f.max && spend(f.cost(l + 1))) { run.fort[d.id]++; if (d.id === 'reinforce') run.wallHp += BALANCE.wallHpPerLevel; }
    },
    slot(d) {
      const run = Game.run, s = SLOTS[d.id], cur = run.slots[d.id];
      if (cur < s.max && spend(s.cost[cur - s.start])) run.slots[d.id]++;
    },
    hire(d) { const run = Game.run; if (run.crew.length < run.slots.crew && spend(CREW[d.id].price)) run.crew.push({ id: d.id, lvl: 1, spent: 0 }); },
    lvlCrew(d) { const c = Game.run.crew[+d.i], k = BALANCE.crewLevelCost(CREW[c.id].price, c.lvl); if (c.lvl < BALANCE.crewMaxLevel && spend(k)) { c.lvl++; c.spent += k; } },
    dismiss(d) { const run = Game.run, c = run.crew[+d.i]; run.scrap += refund(CREW[c.id].price + c.spent); run.crew.splice(+d.i, 1); Sfx.play('coin'); },
    buyT(d) { if (spend(TRAPS[d.id].price)) Game.run.traps[d.id] = (Game.run.traps[d.id] || 0) + 1; },
    sellT(d) { const run = Game.run; if ((run.traps[d.id] || 0) - trapsPlaced(run, d.id) > 0) { run.traps[d.id]--; run.scrap += refund(TRAPS[d.id].price); Sfx.play('coin'); } },
    place() { enterPlace(); return 'keep'; },
    placeSel(d) { Game.placeSel = d.id; Sfx.play('click'); showPlaceBar(); return 'keep'; },
    placeClear() { Game.run.placed = []; showPlaceBar(); return 'keep'; },
    placeDone() { Game.placeSel = null; Game.state = 'shop'; showShop(); return 'keep'; },
    begin() {
      if (!Game.run.loadout.weapons.slice(0, Game.run.slots.weapon).some(Boolean)) { flash('Equip a weapon first'); Sfx.play('deny'); return; }
      showCards(); return 'keep';
    },
    back() { showShop(); return 'keep'; },
    card(d) { hide(); offer = null; startNight(d.id); return 'keep'; },
    resume() { Game.paused = false; hide(); return 'keep'; },
    quit() { Game.paused = false; Game.N = null; showTitle(); return 'keep'; },
    toShop() { showShop(); return 'keep'; },
    endless() { Game.run.endless = true; saveRun(); showSummary(lastSummary); return 'keep'; },
    mute() { const m = Sfx.toggleMute(); flash(m ? 'Sound off' : 'Sound on'); return 'keep'; },
  };

  function autoEquipW(id) {
    const run = Game.run, L = run.loadout.weapons;
    if (L.includes(id)) return;
    const free = L.slice(0, run.slots.weapon).indexOf(null);
    if (free >= 0) L[free] = id;
  }

  function onClick(ev) {
    const el = ev.target.closest('[data-act]');
    if (!el || el.disabled || el.tagName === 'SELECT') return;
    Sfx.init();
    const act = A[el.dataset.act];
    if (!act) return;
    if (el.dataset.act !== 'card') Sfx.play('click');
    const r = act(el.dataset);
    if (r !== 'keep' && (Game.state === 'shop')) showShop();
  }
  function onChange(ev) {
    const el = ev.target;
    if (el.dataset.act !== 'setMod') return;
    const w = Game.run.weapons[el.dataset.w];
    w.mods[+el.dataset.i] = el.value || null;
    Sfx.play('click');
    showShop();
  }

  // ---- screens -------------------------------------------------------------------
  function showTitle() {
    Game.state = 'title';
    const save = loadRun(), best = bestNight();
    if (!Game.run) Game.run = null;
    $(`<div class="title">
      <div class="logo">TIDE<span>WALL</span></div>
      <div class="tag">Hold the sea wall. Twenty nights. Every shot counts.</div>
      <div class="menu">
        ${btn('new', 'New Run', {}, 'big')}
        ${save ? btn('continue', `Continue — Night ${save.night}`, {}, 'big alt') : ''}
        ${btn('howto', 'How to Play', {}, 'alt')}
      </div>
      ${best ? `<div class="best">Best: survived ${best} night${best === 1 ? '' : 's'}</div>` : ''}
      <div class="foot">Inspired by The Last Stand &amp; Bunny Invasion · Mouse + keyboard</div>
    </div>`);
  }

  function showHowTo(start) {
    $(`<div class="panel how">
      <h2>How to hold the wall</h2>
      <div class="cols">
        <div>
          <h3>Controls</h3>
          <table class="keys">
            <tr><td>Mouse</td><td>Aim · hold <b>Left Click</b> to fire</td></tr>
            <tr><td>R</td><td>Reload. Tap <b>R</b> again inside the <span class="g">green zone</span> for a <b>Perfect</b> reload (+25% damage)</td></tr>
            <tr><td>1 2 3 · Q · Wheel</td><td>Switch weapons</td></tr>
            <tr><td>Space · E</td><td>Gadgets</td></tr>
            <tr><td>Esc · M</td><td>Pause · Mute</td></tr>
          </table>
          <h3>Tips</h3>
          <ul>
            <li>Aim for <b>weak points</b> (eyes, heads, crowns) for ×2 damage.</li>
            <li>Shoot the <b class="gold">golden pearls</b> drifting overhead for bonus scrap.</li>
            <li>Holstered weapons <b>don't reload</b>, unless you hire a Deckhand.</li>
            <li>Kill puffers <b>before</b> they reach the wall, and inside packs.</li>
          </ul>
        </div>
        <div>
          <h3>The tide is the game</h3>
          <p>Everything comes out of the sea <b class="wet">Wet</b> and dries off as it crosses the sand.</p>
          <table class="elem">
            <tr><td class="shock">Shock</td><td>on Wet: ×1.5 damage, chains to 3 → <b>CONDUCTOR</b></td></tr>
            <tr><td class="cold">Cold</td><td>on Wet: instant freeze → <b>FLASH FREEZE</b></td></tr>
            <tr><td class="fire">Fire</td><td>on Wet: just steams them dry. Burns dry targets.</td></tr>
            <tr><td class="fire">Fire</td><td>on Oiled: <b>INFERNO</b> (spreads)</td></tr>
            <tr><td class="fire">Fire</td><td>on Frozen: <b>THERMAL SHOCK</b> (big hit)</td></tr>
            <tr><td class="cold">Kill Frozen</td><td><b>SHATTER</b>: shards chill nearby enemies</td></tr>
          </table>
          <p class="dim">Shock and Cold rule the waterline. Fire rules the wall. Sirens, sprinklers, storms and Tide Surge move the wet line.</p>
        </div>
      </div>
      <div class="row end">${btn('closeHow', start ? 'To the shop ▶' : 'Back', { start: start ? '1' : '0' }, 'big')}</div>
    </div>`);
  }

  function loadoutKeys(run) {
    const k = { weapons: 0 };
    run.loadout.weapons.slice(0, run.slots.weapon).filter(Boolean).forEach(id => {
      const s = weaponStats(run, id);
      k.weapons++;
      s.elements.forEach(e => { k[e] = true; });
      if (s.splash || s.blast) k.explosive = true;
      if (s.knock) k.knock = true;
      if (s.salvage > 1) k.greed = true;
      if (s.headMult > BALANCE.headMult) k.hollow = true;
    });
    const G = { flare: ['fire'], frost: ['cold'], storm: ['shock'], depth: ['explosive'], surge: ['wetSource', 'knock'] };
    run.loadout.gadgets.slice(0, run.slots.gadget).filter(Boolean).forEach(g => (G[g] || []).forEach(x => { k[x] = true; }));
    const T = { oil: 'oil', sprinkler: 'wetSource', post: 'shock', mine: 'explosive' };
    run.placed.forEach(p => { if (T[p.id]) k[T[p.id]] = true; });
    run.crew.forEach(c => { if (c.id === 'spotter') k.spotter = true; if (c.id === 'deckhand') k.deckhand = true; if (c.id === 'scavenger') k.greed = true; });
    return k;
  }

  function statLine(s) {
    const dps = s.kind === 'flame' ? s.dmg * s.rate : s.dmg * s.pellets * s.rate;
    const sustained = (s.mag / s.rate) / (s.mag / s.rate + s.reload) * dps;
    const tags = [];
    s.elements.forEach(e => tags.push(`<span class="tag-el ${e}">${e}</span>`));
    if (s.pierce) tags.push(`<span class="tag-el">pierce ${s.pierce > 50 ? '∞' : s.pierce}</span>`);
    if (s.armorPierce) tags.push(`<span class="tag-el">armor ½</span>`);
    if (s.splash || s.blast) tags.push(`<span class="tag-el">blast</span>`);
    if (s.knock) tags.push(`<span class="tag-el">knockback</span>`);
    if (s.headMult > 2) tags.push(`<span class="tag-el">weak ×${s.headMult}</span>`);
    if (s.salvage > 1) tags.push(`<span class="tag-el gold">salvage</span>`);
    return `<div class="stats">
      <span><b>${Math.round(s.dmg)}${s.pellets > 1 ? '×' + s.pellets : ''}</b> dmg</span>
      <span><b>${s.rate.toFixed(1)}</b>/s</span>
      <span><b>${s.mag}</b> mag</span>
      <span><b>${s.reload.toFixed(1)}</b>s rld</span>
      <span title="sustained damage per second incl. reloads">≈<b>${Math.round(sustained)}</b> dps</span>
    </div><div class="tags">${tags.join('')}</div>`;
  }

  function tabArmory(run) {
    const L = run.loadout.weapons;
    const weapons = Object.entries(WEAPONS).map(([id, def]) => {
      const own = run.weapons[id];
      if (!own) {
        return `<div class="card locked">
          <div class="ch"><b>${def.name}</b><span class="price">${cost(def.price)}</span></div>
          <div class="desc">${def.desc}</div>
          ${statLine(weaponStats({ weapons: { [id]: { tier: 1, mods: [] } } }, id))}
          <div class="row">${btn('buyW', 'Buy', { id }, 'buy', run.scrap < def.price)}</div>
        </div>`;
      }
      const s = weaponStats(run, id);
      const tc = BALANCE.tierCost(def.price, own.tier);
      const sockets = own.mods.map((m, i) => {
        const opts = Object.keys(MODS).filter(mid => mid === m || modsFree(run, mid) > 0)
          .map(mid => `<option value="${mid}" ${mid === m ? 'selected' : ''}>${MODS[mid].name}</option>`).join('');
        return `<select data-act="setMod" data-w="${id}" data-i="${i}" class="sock ${m ? 'full' : ''}"><option value="">— empty socket —</option>${opts}</select>`;
      }).join('');
      const slotBtns = [0, 1, 2].slice(0, run.slots.weapon).map(i => btn('equipW', L[i] === id ? `Slot ${i + 1} ✓` : `Slot ${i + 1}`, { id, slot: i }, L[i] === id ? 'on' : 'ghost')).join('');
      return `<div class="card owned ${L.includes(id) ? 'equipped' : ''}">
        <div class="ch"><b>${def.name}</b><span class="tier">${'◆'.repeat(own.tier)}${'◇'.repeat(BALANCE.tierMax - own.tier)}</span></div>
        <div class="desc">${def.desc}</div>
        ${statLine(s)}
        <div class="row socks">${sockets}${own.sockets < 2 ? btn('socket', `+ socket ${cost(BALANCE.socketCost)}`, { id }, 'ghost small', run.scrap < BALANCE.socketCost) : ''}</div>
        <div class="row">${slotBtns}
          ${own.tier < BALANCE.tierMax ? btn('tierW', `Tier up ${cost(tc)}`, { id }, 'buy small', run.scrap < tc) : '<span class="dim">max tier</span>'}
          ${def.price > 0 || own.spent > 0 ? btn('sellW', `Sell +${refund(def.price + own.spent)}`, { id }, 'ghost small danger') : ''}
        </div>
      </div>`;
    }).join('');
    const mods = Object.entries(MODS).map(([id, m]) => {
      const owned = run.mods[id] || 0, free = modsFree(run, id);
      return `<div class="mod">
        <div><b class="${m.tag || ''}">${m.name}</b> <span class="dim">${owned ? `${free}/${owned} free` : ''}</span><div class="desc">${m.desc}</div></div>
        <div class="row">${btn('buyMod', cost(m.price), { id }, 'buy small', run.scrap < m.price)}${free > 0 ? btn('sellMod', `+${refund(m.price)}`, { id }, 'ghost small danger') : ''}</div>
      </div>`;
    }).join('');
    const gadgets = Object.entries(GADGETS).map(([id, g]) => {
      const owned = run.gadgets[id], GL = run.loadout.gadgets;
      return `<div class="mod ${owned ? 'have' : ''}">
        <div><b>${g.name}</b> <span class="dim">${g.cd}s cooldown</span><div class="desc">${g.desc}</div></div>
        <div class="row">${owned
          ? [0, 1].slice(0, run.slots.gadget).map(i => btn('equipG', GL[i] === id ? `${i ? 'E' : 'Space'} ✓` : (i ? 'E' : 'Space'), { id, slot: i }, GL[i] === id ? 'on small' : 'ghost small')).join('') + btn('sellG', `+${refund(g.price)}`, { id }, 'ghost small danger')
          : btn('buyG', cost(g.price), { id }, 'buy small', run.scrap < g.price)}</div>
      </div>`;
    }).join('');
    return `<div class="split">
      <div class="col wide"><h3>Weapons</h3><div class="grid">${weapons}</div></div>
      <div class="col"><h3>Mods <span class="dim">— socket them into weapons, move them freely</span></h3>${mods}
        <h3>Gadgets</h3>${gadgets}</div>
    </div>`;
  }

  function tabFortify(run) {
    const mx = wallMax(run), missing = Math.ceil(mx - run.wallHp);
    const all = Math.ceil(missing * BALANCE.repairCostPerHp), part = Math.ceil(Math.min(50, missing) * BALANCE.repairCostPerHp);
    const forts = Object.entries(FORTIFY).map(([id, f]) => {
      const l = run.fort[id], c = f.cost(l + 1);
      return `<div class="mod"><div><b>${f.name}</b> <span class="lvl">${l}/${f.max}</span><div class="desc">${f.desc}</div></div>
        <div class="row">${l < f.max ? btn('fort', cost(c), { id }, 'buy small', run.scrap < c) : '<span class="dim">max</span>'}</div></div>`;
    }).join('');
    const slots = Object.entries(SLOTS).map(([id, s]) => {
      const cur = run.slots[id], c = s.cost[cur - s.start];
      return `<div class="mod"><div><b>${s.name}s</b> <span class="lvl">${cur}/${s.max}</span></div>
        <div class="row">${cur < s.max ? btn('slot', `+1 ${cost(c)}`, { id }, 'buy small', run.scrap < c) : '<span class="dim">max</span>'}</div></div>`;
    }).join('');
    return `<div class="split">
      <div class="col"><h3>Wall</h3>
        <div class="wallbar"><div style="width:${100 * run.wallHp / mx}%"></div><span>${Math.ceil(run.wallHp)} / ${mx}</span></div>
        <div class="row">${btn('repair', `Repair 50 HP ${cost(part)}`, { amt: 50 }, 'buy', !missing || run.scrap < part)}
        ${btn('repair', `Repair all ${cost(all)}`, { amt: 'all' }, 'buy', !missing || run.scrap < all)}</div>
        ${forts}</div>
      <div class="col"><h3>Slots</h3>${slots}</div>
    </div>`;
  }

  function tabCrew(run) {
    const crew = run.crew.map((c, i) => {
      const def = CREW[c.id], k = BALANCE.crewLevelCost(def.price, c.lvl);
      return `<div class="mod have"><div><b style="color:${def.color}">${def.name}</b> <span class="lvl">${'★'.repeat(c.lvl)}${'☆'.repeat(BALANCE.crewMaxLevel - c.lvl)}</span><div class="desc">${def.desc}</div></div>
        <div class="row">${c.lvl < BALANCE.crewMaxLevel ? btn('lvlCrew', `Train ${cost(k)}`, { i }, 'buy small', run.scrap < k) : ''}${btn('dismiss', `Dismiss +${refund(def.price + c.spent)}`, { i }, 'ghost small danger')}</div></div>`;
    }).join('') || '<p class="dim">Nobody on the wall but you.</p>';
    const full = run.crew.length >= run.slots.crew;
    const hire = Object.entries(CREW).map(([id, def]) => `<div class="mod"><div><b style="color:${def.color}">${def.name}</b><div class="desc">${def.desc}</div></div>
      <div class="row">${btn('hire', `Hire ${cost(def.price)}`, { id }, 'buy small', full || run.scrap < def.price)}</div></div>`).join('');
    return `<div class="split">
      <div class="col"><h3>On the wall <span class="dim">${run.crew.length}/${run.slots.crew} posts</span></h3>${crew}</div>
      <div class="col"><h3>Hire ${full ? '<span class="dim">— all posts filled (buy more in Fortify)</span>' : ''}</h3>${hire}</div>
    </div>`;
  }

  function tabBeach(run) {
    const traps = Object.entries(TRAPS).map(([id, t]) => {
      const owned = run.traps[id] || 0, placed = trapsPlaced(run, id);
      return `<div class="mod ${owned ? 'have' : ''}"><div><b>${t.name}</b> <span class="dim">${owned ? `${placed}/${owned} placed` : ''}</span><div class="desc">${t.desc}</div></div>
        <div class="row">${btn('buyT', cost(t.price), { id }, 'buy small', run.scrap < t.price)}${owned - placed > 0 ? btn('sellT', `+${refund(t.price)}`, { id }, 'ghost small danger') : ''}</div></div>`;
    }).join('');
    return `<div class="split">
      <div class="col"><h3>Traps <span class="dim">${run.placed.length}/${run.slots.trap} slots used</span></h3>${traps}</div>
      <div class="col"><h3>The beach</h3>
        <p>Traps sit on the sand and hit only ground enemies. Flyers and leaping eels skip them.</p>
        <p class="dim">Think about the wet line: a Shock Post near the surf hits wet targets, an Oil Slick near the wall catches dry ones for fire, and a Brine Sprinkler drags the wet zone right up to your wall.</p>
        <div class="row">${btn('place', 'Place traps on the beach ▶', {}, 'big', !Object.values(run.traps).some(Boolean))}</div>
      </div>
    </div>`;
  }

  function footer(run) {
    const L = run.loadout.weapons.slice(0, run.slots.weapon);
    const wl = L.map((id, i) => id
      ? `<div class="lo"><span class="k">${i + 1}</span><b>${WEAPONS[id].name}</b><small>${run.weapons[id].mods.filter(Boolean).map(m => MODS[m].name).join(' · ') || '—'}</small></div>`
      : `<div class="lo empty"><span class="k">${i + 1}</span>empty</div>`).join('');
    const gl = run.loadout.gadgets.slice(0, run.slots.gadget).map((id, i) => `<div class="lo ${id ? '' : 'empty'}"><span class="k">${i ? 'E' : '␣'}</span>${id ? `<b>${GADGETS[id].name}</b>` : 'empty'}</div>`).join('');
    const k = loadoutKeys(run);
    const syn = SYNERGIES.filter(s => s.test(k)).map(s => `<span class="syn" title="${s.desc}">${s.name}</span>`).join('');
    return `<div class="footer">
      <div class="loadout">${wl}${gl}
        <div class="lo mini"><b>Crew</b><small>${run.crew.map(c => CREW[c.id].name).join(', ') || '—'}</small></div>
        <div class="lo mini"><b>Traps</b><small>${run.placed.length}/${run.slots.trap} placed</small></div>
      </div>
      <div class="synrow">${syn || '<span class="dim">No synergies yet. Mix elements with wet sources, oil or explosives.</span>'}</div>
      ${btn('begin', `Begin Night ${run.night} ▶`, {}, 'big go')}
    </div>`;
  }

  function showShop() {
    Game.state = 'shop';
    const run = Game.run;
    const scroll = root.querySelector('.content');
    const st = scroll ? scroll.scrollTop : 0;
    saveRun();
    const boss = bossFor(run.night);
    const tabs = [['armory', 'Armory'], ['fortify', 'Fortify'], ['crew', 'Crew'], ['beach', 'Beach']]
      .map(([id, n]) => `<button class="tab ${tab === id ? 'on' : ''}" data-act="tab" data-tab="${id}">${n}</button>`).join('');
    const body = tab === 'armory' ? tabArmory(run) : tab === 'fortify' ? tabFortify(run) : tab === 'crew' ? tabCrew(run) : tabBeach(run);
    $(`<div class="shop">
      <div class="header">
        <div class="h-night">Night ${run.night}${boss ? ` <span class="boss">BOSS: ${ENEMIES[boss].name}</span>` : ''}</div>
        <div class="tabs">${tabs}</div>
        <div class="h-wall">Wall ${Math.ceil(run.wallHp)}/${wallMax(run)}</div>
        <div class="h-scrap">⚙ ${run.scrap}</div>
        <button class="btn ghost small" data-act="title">Menu</button>
      </div>
      <div class="content">${body}</div>
      ${footer(run)}
      <div id="toast"></div>
    </div>`);
    const c = root.querySelector('.content');
    if (c) c.scrollTop = st;
  }

  function enterPlace() {
    Game.state = 'place';
    const run = Game.run;
    Game.placeSel = Object.keys(TRAPS).find(id => (run.traps[id] || 0) - trapsPlaced(run, id) > 0) || null;
    showPlaceBar();
  }
  function showPlaceBar() {
    const run = Game.run;
    const items = Object.entries(TRAPS).filter(([id]) => run.traps[id]).map(([id, t]) => {
      const left = run.traps[id] - trapsPlaced(run, id);
      return btn('placeSel', `${t.name} ×${left}`, { id }, Game.placeSel === id ? 'on small' : 'ghost small', left <= 0);
    }).join('');
    root.classList.add('show');
    root.innerHTML = `<div class="placebar">
      <div><b>Placing traps</b> <span class="dim">${run.placed.length}/${run.slots.trap} slots · click the sand to place · click a trap to pick it up</span></div>
      <div class="row">${items}${btn('placeClear', 'Clear all', {}, 'ghost small danger')}${btn('placeDone', 'Done ✓', {}, 'buy')}</div>
    </div>`;
  }
  function placeClick(x) {
    const run = Game.run;
    const near = run.placed.findIndex(p => Math.abs(p.x - x) < Math.max(18, TRAPS[p.id].w / 2));
    if (near >= 0) { Game.placeSel = run.placed[near].id; run.placed.splice(near, 1); Sfx.play('click'); showPlaceBar(); return; }
    const id = Game.placeSel;
    if (!id) return;
    if (run.placed.length >= run.slots.trap) { Sfx.play('deny'); return; }
    if ((run.traps[id] || 0) - trapsPlaced(run, id) <= 0) { Sfx.play('deny'); return; }
    run.placed.push({ id, x: clamp(x, FIELD.trapMinX, FIELD.trapMaxX) });
    Sfx.play('buy');
    if ((run.traps[id] || 0) - trapsPlaced(run, id) <= 0) Game.placeSel = Object.keys(TRAPS).find(t => (run.traps[t] || 0) - trapsPlaced(run, t) > 0) || null;
    showPlaceBar();
  }

  function rollCards(n) {
    const ids = Object.keys(CARDS).filter(id => id !== 'calm' && !(n < 3 && (id === 'red' || id === 'iron')));
    const out = [];
    while (out.length < 3) { const c = pick(ids); if (!out.includes(c)) out.push(c); }
    if (!out.some(id => CARDS[id].mult <= 1.1)) out[randi(0, 2)] = 'calm';
    return out;
  }
  function showCards() {
    Game.state = 'cards';
    const run = Game.run;
    if (!offer || offer.n !== run.night) offer = { n: run.night, ids: rollCards(run.night) };
    const boss = bossFor(run.night);
    $(`<div class="panel cards">
      <h2>Night ${run.night}: read the tide</h2>
      ${boss ? `<div class="bosswarn">The ${ENEMIES[boss].name} comes tonight.</div>` : ''}
      <div class="cardrow">${offer.ids.map(id => {
        const c = CARDS[id];
        return `<div class="tcard" data-act="card" data-id="${id}">
          <div class="ic">${c.icon}</div><div class="nm">${c.name}</div><div class="ds">${c.desc}</div>
          <div class="ml ${c.mult > 1 ? 'up' : ''}">${c.mult > 1 ? '×' + c.mult.toFixed(2) + ' scrap' : 'normal scrap'}</div>
        </div>`;
      }).join('')}</div>
      <div class="row end">${btn('back', '◀ Back to shop', {}, 'ghost')}</div>
    </div>`);
  }

  function showSummary(s) {
    lastSummary = s;
    Game.state = 'summary';
    $(`<div class="panel summary">
      <h2>Dawn breaks: night ${s.n} survived</h2>
      <div class="cols">
        <div>
          <div class="kv"><span>Kills</span><b>${s.kills}</b></div>
          <div class="kv"><span>Weak point hits</span><b>${s.heads}</b></div>
          <div class="kv"><span>Perfect reloads</span><b>${s.perfects}</b></div>
          <div class="kv"><span>Pearls shot</span><b>${s.pearlsHit}</b></div>
          <div class="kv"><span>Wall damage taken</span><b>${Math.max(0, s.wallLost)}</b></div>
          ${s.repaired ? `<div class="kv"><span>Mechanics repaired</span><b>+${s.repaired}</b></div>` : ''}
        </div>
        <div>
          <div class="kv"><span>Bounties</span><b>${s.bounty}</b></div>
          <div class="kv"><span>Night bonus (wall ${Math.round(s.pct * 100)}%)</span><b>${s.bonus}</b></div>
          ${s.pearls ? `<div class="kv"><span>Pearls</span><b>${s.pearls}</b></div>` : ''}
          ${s.scav ? `<div class="kv"><span>Scavengers</span><b>${s.scav}</b></div>` : ''}
          <div class="kv"><span>${s.card.icon} ${s.card.name}</span><b>×${s.card.mult.toFixed(2)}</b></div>
          <div class="kv total"><span>Scrap earned</span><b>⚙ ${s.total}</b></div>
        </div>
      </div>
      <div class="row end">${btn('toShop', 'To the shop ▶', {}, 'big')}</div>
    </div>`);
  }

  function showVictory(s) {
    lastSummary = s;
    Game.state = 'summary';
    const run = Game.run;
    $(`<div class="panel summary victory">
      <h2>The Leviathan is slain</h2>
      <p>You held the Tidewall for ${s.n} nights. ${run.stats.kills} creatures went back to the deep.</p>
      <div class="kv"><span>Weak point hits</span><b>${run.stats.heads}</b></div>
      <div class="kv"><span>Perfect reloads</span><b>${run.stats.perfects}</b></div>
      <div class="kv total"><span>Scrap earned (run)</span><b>⚙ ${run.stats.earned}</b></div>
      <div class="row end">${btn('title', 'Retire', {}, 'ghost')}${btn('endless', 'The tide keeps coming… (Endless) ▶', {}, 'big')}</div>
    </div>`);
  }

  function showGameOver(i) {
    $(`<div class="panel summary over">
      <h2>The wall has fallen</h2>
      <p>You held out until <b>night ${i.night}</b>.</p>
      <div class="cols"><div>
        <div class="kv"><span>Kills</span><b>${i.kills}</b></div>
        <div class="kv"><span>Weak point hits</span><b>${i.heads}</b></div>
        <div class="kv"><span>Perfect reloads</span><b>${i.perfects}</b></div>
      </div><div>
        <div class="kv"><span>Most wall damage</span><b>${esc(i.cause)} (${i.causeDmg})</b></div>
        <div class="kv"><span>Final blow</span><b>${esc(i.last || '—')}</b></div>
        <div class="kv"><span>Best run</span><b>${bestNight()} nights</b></div>
      </div></div>
      <p class="dim">Tip: ${tipFor(i.cause)}</p>
      <div class="row end">${btn('title', 'Title', {}, 'ghost')}${btn('new', 'New Run ▶', {}, 'big')}</div>
    </div>`);
  }
  function tipFor(cause) {
    return ({
      Crabling: 'spread weapons, Spike Strips and shock chains thin out crab swarms.',
      Drowner: 'aim for the glowing eyes. Weak points do double damage.',
      Gull: 'gulls ignore traps. Hitscan weapons, the SMG and Riflemen handle flyers.',
      Puffer: 'pop puffers early, ideally inside a pack, before they reach the wall.',
      Eel: 'eels leap over traps. Use spread or chain damage.',
      Shellback: 'armor eats small hits. Use pierce, explosions or freezing.',
      Siren: 'sirens heal and soak everything. Reach them with the Longrifle or Mortar.',
      Clawback: 'mark it with a Spotter and set up a Thermal Shock (cold, then fire).',
      'Crab King': 'split your loadout: one weapon for the King, one for its crablings.',
      'Siren Queen': 'the whole beach is wet during her songs, so a Shock build is ideal.',
      Leviathan: 'its glowing eye is the weak point. Bring everything you have.',
    })[cause] || 'try a different loadout. Every enemy has a counter.';
  }

  function showPause() {
    $(`<div class="panel pause"><h2>Paused</h2>
      <div class="menu">${btn('resume', 'Resume', {}, 'big')}${btn('mute', Sfx.muted ? 'Unmute' : 'Mute', {}, 'alt')}${btn('quit', 'Quit to title (progress saved at last dawn)', {}, 'ghost')}</div></div>`);
  }

  function init(el) {
    root = el;
    root.addEventListener('click', onClick);
    root.addEventListener('change', onChange);
  }

  return { init, showTitle, showShop, showSummary, showVictory, showGameOver, showPause, showCards, placeClick, hide, flash };
})();
