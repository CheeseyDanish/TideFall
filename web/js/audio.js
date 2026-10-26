// Tidewall — synthesized sound effects (no asset files, R15).
'use strict';

const Sfx = (() => {
  let ctx = null, master = null, noiseBuf = null, muted = false;
  const last = {};

  function init() {
    if (ctx) { if (ctx.state === 'suspended') ctx.resume(); return; }
    try {
      ctx = new (window.AudioContext || window.webkitAudioContext)();
      master = ctx.createGain();
      master.gain.value = 0.35;
      master.connect(ctx.destination);
      noiseBuf = ctx.createBuffer(1, ctx.sampleRate, ctx.sampleRate);
      const d = noiseBuf.getChannelData(0);
      for (let i = 0; i < d.length; i++) d[i] = Math.random() * 2 - 1;
    } catch (e) { ctx = null; }
  }

  // Throttle identical sounds so SMG/flame spam doesn't clip.
  function ok(name, gap) {
    if (!ctx || muted) return false;
    const t = ctx.currentTime;
    if (last[name] && t - last[name] < gap) return false;
    last[name] = t;
    return true;
  }

  function env(g, t, a, peak, d) {
    g.gain.setValueAtTime(0.0001, t);
    g.gain.exponentialRampToValueAtTime(peak, t + a);
    g.gain.exponentialRampToValueAtTime(0.0001, t + a + d);
  }

  function noise(dur, freq, q, peak, type = 'bandpass') {
    const t = ctx.currentTime;
    const src = ctx.createBufferSource();
    src.buffer = noiseBuf;
    const f = ctx.createBiquadFilter();
    f.type = type; f.frequency.value = freq; f.Q.value = q;
    const g = ctx.createGain();
    env(g, t, 0.003, peak, dur);
    src.connect(f); f.connect(g); g.connect(master);
    src.start(t, Math.random() * 0.5); src.stop(t + dur + 0.05);
  }

  function tone(freq, dur, type, peak, slideTo) {
    const t = ctx.currentTime;
    const o = ctx.createOscillator();
    o.type = type; o.frequency.setValueAtTime(freq, t);
    if (slideTo) o.frequency.exponentialRampToValueAtTime(slideTo, t + dur);
    const g = ctx.createGain();
    env(g, t, 0.005, peak, dur);
    o.connect(g); g.connect(master);
    o.start(t); o.stop(t + dur + 0.05);
  }

  const P = {
    shot(kind) {
      if (kind === 'flame') { if (ok('flame', 0.07)) noise(0.12, 900, 0.6, 0.25, 'lowpass'); return; }
      if (kind === 'arc') { if (ok('arc', 0.05)) { tone(1400, 0.08, 'sawtooth', 0.12, 300); noise(0.06, 4000, 1, 0.15); } return; }
      if (kind === 'lob') { if (ok('lob', 0.05)) { tone(160, 0.18, 'sine', 0.4, 60); noise(0.1, 500, 1, 0.3); } return; }
      if (kind === 'heavy') { if (ok('heavy', 0.05)) { noise(0.25, 700, 0.7, 0.6, 'lowpass'); tone(110, 0.2, 'square', 0.15, 50); } return; }
      if (ok('shot', 0.035)) { noise(0.09, 1800, 0.8, 0.4); tone(220, 0.05, 'square', 0.08, 90); }
    },
    hit() { if (ok('hit', 0.03)) noise(0.04, 2500, 2, 0.15); },
    head() { if (ok('head', 0.04)) tone(1760, 0.09, 'triangle', 0.2, 2400); },
    kill() { if (ok('kill', 0.03)) { noise(0.12, 600, 1, 0.25); } },
    boom(size = 1) { if (ok('boom', 0.06)) { noise(0.5 * size, 180, 0.6, 0.8, 'lowpass'); tone(80, 0.4 * size, 'sine', 0.5, 30); } },
    zap() { if (ok('zap', 0.04)) { tone(900 + Math.random() * 600, 0.1, 'sawtooth', 0.12, 200); } },
    freeze() { if (ok('freeze', 0.06)) { tone(2400, 0.2, 'sine', 0.12, 1200); noise(0.15, 6000, 3, 0.1); } },
    combo() { if (ok('combo', 0.08)) { tone(660, 0.12, 'triangle', 0.2, 1320); } },
    wall() { if (ok('wall', 0.12)) { noise(0.18, 220, 1, 0.5, 'lowpass'); } },
    reload() { if (ok('reload', 0.1)) { noise(0.04, 3000, 4, 0.2); } },
    load() { if (ok('load', 0.04)) { noise(0.03, 4200, 5, 0.18); tone(1100, 0.03, 'square', 0.06); } },   // one round in
    tick() { if (ok('tick', 0.05)) tone(1600, 0.025, 'square', 0.05); },                                   // rhythm metronome
    perfect() { if (ok('perfect', 0.1)) { tone(880, 0.08, 'triangle', 0.25); setTimeout(() => ctx && tone(1320, 0.12, 'triangle', 0.25), 70); } },
    jam() { if (ok('jam', 0.1)) { tone(140, 0.2, 'square', 0.2, 90); } },
    coin() { if (ok('coin', 0.05)) { tone(1320, 0.07, 'square', 0.12); setTimeout(() => ctx && tone(1980, 0.12, 'square', 0.12), 60); } },
    click() { if (ok('click', 0.03)) tone(600, 0.04, 'triangle', 0.15); },
    buy() { if (ok('buy', 0.05)) { tone(523, 0.08, 'triangle', 0.2); setTimeout(() => ctx && tone(784, 0.12, 'triangle', 0.2), 70); } },
    deny() { if (ok('deny', 0.1)) tone(180, 0.15, 'square', 0.15, 120); },
    splash() { if (ok('splash', 0.1)) noise(0.3, 1200, 0.5, 0.15, 'lowpass'); },
    gull() { if (ok('gull', 0.5)) tone(1300, 0.25, 'sawtooth', 0.06, 900); },
    sing() { if (ok('sing', 0.8)) { tone(523, 0.6, 'sine', 0.08, 659); } },
    horn() { if (ok('horn', 1)) { tone(98, 1.2, 'sawtooth', 0.18, 92); tone(147, 1.2, 'sawtooth', 0.1, 140); } },
    dawn() { if (ok('dawn', 1)) { [523, 659, 784, 1047].forEach((f, i) => setTimeout(() => ctx && tone(f, 0.3, 'triangle', 0.18), i * 110)); } },
  };

  return {
    init,
    play(name, ...args) { if (ctx && !muted && P[name]) P[name](...args); },
    toggleMute() { muted = !muted; return muted; },
    get muted() { return muted; },
  };
})();
