// Tidewall — per-weapon reload minigames. Styles and their numbers live in RELOADS (data.js).
// Every style is optional: with no input, a reload finishes in the weapon's reload time (CLAUDE.md R13).
//
// State lives on the weapon slot as `a.rl` while `a.reloading` is true. game.js calls:
//   Reload.start(a)              when a reload begins
//   Reload.update(a, dt, idle)   every frame; returns true when the reload is complete
//                                (idle = holstered with a Deckhand: passive progress only)
//   Reload.press(a) / release(a) on R down / R up
//   Reload.draw(ctx, a, x, y)    the widget under the crosshair
// Progress for the HUD bar is mirrored into a.rt (0..1) with a.rTotal = 1.
'use strict';

const Reload = (() => {
  const styleOf = a => a.s.def.reloadStyle || 'zone';
  const winMult = () => hasRelic('sirensEar') ? RELICS.sirensEar.windowMult : 1;
  // Widen a [lo, hi] zone around its centre by the Siren's Ear multiplier
  function widen(z) {
    const m = winMult(), c = (z[0] + z[1]) / 2, h = (z[1] - z[0]) / 2 * m;
    return [Math.max(0, c - h), Math.min(1, c + h)];
  }
  const inZone = (v, z) => v >= z[0] && v <= z[1];

  function start(a) {
    const style = styleOf(a), R = RELOADS[style];
    const r = { style, R, t: 0, real: 0, need: a.s.reload, total: a.s.reload, tried: false, jam: false, done: false, bonus: 0 };
    if (style === 'drop') { const w = Math.min(0.5, R.width * winMult()); const lo = rand(0.22, 0.88 - w); r.zone = [lo, lo + w]; }
    else if (R.zone) r.zone = widen(R.zone);
    if (R.zones) { r.zones = R.zones.map(widen); r.step = 0; }
    if (R.perRound) {
      r.per = a.s.reload / a.s.mag;   // passive time per round
      r.roundT = 0; r.fumble = 0; r.clean = true; r.passive = 0;
      r.beatT = -0.35; r.lastBeat = -1; r.usedBeat = -1;
      // the revolver's lead-in before the first beat also delays its passive loading
      if (style === 'rhythm') r.roundT = r.beatT;
    }
    if (style === 'pressure') { r.hold = false; r.needle = 0; }
    a.rl = r;
    a.rt = 0; a.rTotal = 1;
  }

  function perfectBonus(R) {
    return (R.bonus || BALANCE.perfectBonus) + (hasRelic('goldenChamber') ? RELICS.goldenChamber.perfectBonus : 0);
  }
  function succeed(a) { a.rl.done = true; a.rl.bonus = perfectBonus(a.rl.R); }
  function jam(a, secs, word = 'JAMMED') {
    const r = a.rl;
    r.need += secs; r.jam = true; r.tried = true;
    text(Game.input.mx, Game.input.my + 44, word, '#f87171', 15, 0.8);
    Sfx.play('jam');
  }

  function loadRound(a, byTap) {
    const r = a.rl;
    if (a.ammo >= a.s.mag) return;
    a.ammo++;
    r.roundT = 0;
    if (!byTap) r.passive++;
    Sfx.play('load');
  }

  function update(a, dt, idle) {
    const r = a.rl;
    if (!r) return true;
    const R = r.R;
    r.real += dt;
    if (R.perRound) {
      if (r.fumble > 0) r.fumble -= dt;
      else r.roundT += dt;
      if (r.roundT >= r.per) loadRound(a, false);
      if (r.style === 'rhythm' && !idle) {
        r.beatT += dt;
        const k = Math.floor(r.beatT / R.beat);
        if (k >= 0 && k !== r.lastBeat) { r.lastBeat = k; Sfx.play('tick'); }
      }
      a.rt = a.ammo / a.s.mag;
      if (a.ammo >= a.s.mag) {
        // Perfect: every round went in by skill, with no fumbles
        if (!idle && r.clean && r.passive === 0) r.bonus = perfectBonus(R);
        return true;
      }
      return false;
    }
    if (r.style === 'pressure' && !idle) {
      if (r.hold) {
        r.needle += dt / R.fill;
        if (r.needle >= 1) {
          r.hold = false; r.needle = 0;
          burst(FIELD.gunner.x + 30, FIELD.gunner.y, 'rgba(230,230,240,0.7)', 12, 120, 6, 0.8, -60, 'smoke');
          jam(a, R.jam, 'VENTED!');
        }
      } else r.needle = Math.max(0, r.needle - dt * 2.5);
    }
    r.t += dt;
    a.rt = Math.min(1, r.t / r.need);
    if (r.done) return true;
    if (r.t >= r.need) {
      if (r.style === 'mash' && r.mashed && r.real <= R.perfectUnder * r.total) r.bonus = perfectBonus(R);
      return true;
    }
    return false;
  }

  // Bar position for styles that show a moving marker (heavy accelerates: position = progress²)
  function markerPos(r) {
    const p = Math.min(1, r.t / r.need);
    return r.style === 'heavy' ? p * p : p;
  }

  function press(a) {
    const r = a.rl;
    if (!r || r.done) return;
    const R = r.R;
    switch (r.style) {
      case 'zone': case 'drop': case 'heavy': {
        if (r.tried) return;
        r.tried = true;
        if (inZone(markerPos(r), r.zone)) succeed(a); else jam(a, R.jam);
        return;
      }
      case 'bolt': {
        if (r.tried) return;
        const z = r.zones[r.step], p = markerPos(r);
        if (!inZone(p, z)) { jam(a, R.jam); return; }
        Sfx.play('reload');
        if (r.step === 0) { r.step = 1; r.t = Math.max(r.t, 0.5 * r.need); }   // bolt back: skip ahead
        else succeed(a);
        return;
      }
      case 'mash': {
        r.mashed = true;
        r.t += R.perTap * winMult() * r.need;
        burst(FIELD.gunner.x + 30, FIELD.gunner.y - 4, '#a5f3fc', 3, 90, 2, 0.25, 0, 'spark');
        Sfx.play('zap');
        return;
      }
      case 'pressure': {
        if (!r.tried) r.hold = true;
        return;
      }
      case 'rhythm': {
        const ph = ((r.beatT % R.beat) + R.beat) % R.beat;
        const nearest = Math.round(r.beatT / R.beat);
        if (nearest === r.usedBeat) return;                 // one round per beat; double taps are ignored
        if (Math.min(ph, R.beat - ph) <= R.window * winMult() && r.beatT > -R.window) {
          r.usedBeat = nearest;
          loadRound(a, true);
        } else {
          r.fumble = R.fumble; r.clean = false;
          text(Game.input.mx, Game.input.my + 44, 'FUMBLE', '#fca5a5', 13, 0.6);
          Sfx.play('jam');
        }
        return;
      }
      case 'shells': {
        const c = r.roundT / r.per;
        if (inZone(c, r.zone)) loadRound(a, true);
        else {
          r.roundT -= R.slip; r.clean = false;
          text(Game.input.mx, Game.input.my + 44, 'SLIP', '#fca5a5', 13, 0.6);
          Sfx.play('jam');
        }
        return;
      }
    }
  }

  function release(a) {
    const r = a.rl;
    if (!r || r.style !== 'pressure' || !r.hold) return;
    r.hold = false;
    if (inZone(r.needle, r.zone)) succeed(a);
    // released too early: the needle just falls back, try again
  }

  // ---- drawing (all relative to the crosshair) ----
  function bar(ctx, x, y, w, h) {
    ctx.fillStyle = 'rgba(0,0,0,0.7)'; ctx.fillRect(x - 2, y - 2, w + 4, h + 4);
  }
  function zoneRect(ctx, x, y, w, h, z, color = 'rgba(134,239,172,0.85)') {
    ctx.fillStyle = color; ctx.fillRect(x + w * z[0], y, w * (z[1] - z[0]), h);
  }
  function marker(ctx, x, y, h, color) {
    ctx.fillStyle = color; ctx.fillRect(x - 1.5, y - 3, 3, h + 6);
  }
  function caption(ctx, x, y, str, color = 'rgba(229,231,235,0.8)') {
    ctx.font = '700 10px "Trebuchet MS", sans-serif'; ctx.textAlign = 'center';
    ctx.lineWidth = 3; ctx.strokeStyle = 'rgba(0,0,0,0.7)'; ctx.strokeText(str, x, y);
    ctx.fillStyle = color; ctx.fillText(str, x, y);
  }

  function draw(ctx, a, cx, cy) {
    const r = a.rl;
    if (!r) return;
    const R = r.R, w = 96, h = 8, x = cx - w / 2, y = cy;
    const mk = r.jam ? '#ef4444' : '#e5e7eb';
    switch (r.style) {
      case 'zone': case 'drop': case 'heavy': {
        bar(ctx, x, y, w, h);
        if (!r.tried) zoneRect(ctx, x, y, w, h, r.zone, r.style === 'heavy' ? 'rgba(250,204,21,0.9)' : undefined);
        marker(ctx, x + w * markerPos(r), y, h, mk);
        caption(ctx, cx, y + h + 13, r.style === 'heavy' ? 'HEAVY · one chance' : R.name.toUpperCase());
        break;
      }
      case 'bolt': {
        bar(ctx, x, y, w, h);
        if (!r.tried) r.zones.forEach((z, i) => { if (i >= r.step) zoneRect(ctx, x, y, w, h, z, i === r.step ? undefined : 'rgba(134,239,172,0.35)'); });
        marker(ctx, x + w * markerPos(r), y, h, mk);
        caption(ctx, cx, y + h + 13, r.step === 0 ? 'BOLT · open' : 'BOLT · close');
        break;
      }
      case 'mash': {
        bar(ctx, x, y, w, h);
        const g = ctx.createLinearGradient(x, 0, x + w, 0);
        g.addColorStop(0, '#0ea5e9'); g.addColorStop(1, '#a5f3fc');
        ctx.fillStyle = g; ctx.fillRect(x, y, w * Math.min(1, r.t / r.need), h);
        caption(ctx, cx, y + h + 13, 'MASH R', Math.sin(r.real * 16) > 0 ? '#a5f3fc' : '#e0f2fe');
        break;
      }
      case 'pressure': {
        bar(ctx, x, y, w, h);
        ctx.fillStyle = 'rgba(239,68,68,0.85)'; ctx.fillRect(x + w * 0.94, y, w * 0.06, h);
        if (!r.tried) zoneRect(ctx, x, y, w, h, r.zone);
        ctx.fillStyle = 'rgba(251,146,60,0.55)'; ctx.fillRect(x, y, w * r.needle, h);
        marker(ctx, x + w * r.needle, y, h, mk);
        // thin line underneath: the plain reload timer
        ctx.fillStyle = 'rgba(148,163,184,0.8)'; ctx.fillRect(x, y + h + 3, w * Math.min(1, r.t / r.need), 2);
        caption(ctx, cx, y + h + 16, r.tried ? 'VENTED · wait it out' : 'HOLD R · release in green');
        break;
      }
      case 'rhythm': {
        // Approach ring closing on the beat light, then the six chambers
        const ph = ((r.beatT % R.beat) + R.beat) % R.beat;
        const toNext = (R.beat - ph) / R.beat;                 // 1 → 0 as the next beat arrives
        const near = 1 - Math.min(ph, R.beat - ph) / (R.beat / 2);
        const bx = cx, by = y + 2;
        ctx.strokeStyle = r.fumble > 0 ? 'rgba(248,113,113,0.8)' : 'rgba(253,224,71,0.8)'; ctx.lineWidth = 2;
        ctx.beginPath(); ctx.arc(bx, by, 5 + 12 * toNext, 0, Math.PI * 2); ctx.stroke();
        ctx.fillStyle = r.fumble > 0 ? '#ef4444' : `rgba(253,224,71,${0.25 + 0.75 * Math.pow(Math.max(0, near), 3)})`;
        ctx.beginPath(); ctx.arc(bx, by, 5, 0, Math.PI * 2); ctx.fill();
        pips(ctx, a, cx, y + 24);
        caption(ctx, cx, y + 42, 'RHYTHM · tap R on the beat');
        break;
      }
      case 'shells': {
        bar(ctx, x, y, w, h);
        if (r.zone) zoneRect(ctx, x, y, w, h, r.zone);
        marker(ctx, x + w * Math.max(0, Math.min(1, r.roundT / r.per)), y, h, '#e5e7eb');
        pips(ctx, a, cx, y + h + 12);
        caption(ctx, cx, y + h + 30, 'SHELLS · tap R in green · fire to stop');
        break;
      }
    }
  }
  function pips(ctx, a, cx, cy) {
    const n = a.s.mag, gap = Math.min(13, 110 / n), x0 = cx - (n - 1) * gap / 2;
    for (let i = 0; i < n; i++) {
      ctx.beginPath(); ctx.arc(x0 + i * gap, cy, 4, 0, Math.PI * 2);
      if (i < a.ammo) { ctx.fillStyle = '#fbbf24'; ctx.fill(); }
      else { ctx.strokeStyle = 'rgba(229,231,235,0.6)'; ctx.lineWidth = 1.5; ctx.stroke(); }
    }
  }

  return { start, update, press, release, draw, styleOf };
})();
