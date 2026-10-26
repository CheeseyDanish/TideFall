// Tidewall — boot, scaling, input, main loop.
'use strict';

(() => {
  const stage = document.getElementById('stage');
  const canvas = document.getElementById('game');
  const ctx = canvas.getContext('2d');
  let scale = 1;

  function fit() {
    scale = Math.min(window.innerWidth / W, window.innerHeight / H);
    const dpr = window.devicePixelRatio || 1;
    stage.style.transform = `translate(-50%, -50%) scale(${scale})`;
    canvas.width = Math.round(W * scale * dpr);
    canvas.height = Math.round(H * scale * dpr);
    ctx.setTransform(scale * dpr, 0, 0, scale * dpr, 0, 0);
  }
  window.addEventListener('resize', fit);
  fit();

  function toLogical(ev) {
    const r = canvas.getBoundingClientRect();
    return { x: (ev.clientX - r.left) / r.width * W, y: (ev.clientY - r.top) / r.height * H };
  }
  window.addEventListener('mousemove', ev => { const p = toLogical(ev); Game.input.mx = p.x; Game.input.my = p.y; });
  canvas.addEventListener('mousedown', ev => {
    Sfx.init();
    const p = toLogical(ev);
    Game.input.mx = p.x; Game.input.my = p.y;
    if (ev.button !== 0) return;
    if (Game.state === 'place') { UI.placeClick(p.x); return; }
    Game.input.down = true;
    Game.input.tap = 0.35;   // buffer: a click shorter than a frame, or during the gun's cooldown, still fires
  });
  window.addEventListener('mouseup', ev => { if (ev.button === 0) Game.input.down = false; });
  canvas.addEventListener('contextmenu', ev => ev.preventDefault());
  window.addEventListener('blur', () => { Game.input.down = false; if (Game.state === 'night' && !Game.paused) pause(); });
  canvas.addEventListener('wheel', ev => {
    const N = Game.N; if (!N || Game.paused) return;
    ev.preventDefault();
    const n = N.arms.length; if (n < 2) return;
    switchTo((N.cur + (ev.deltaY > 0 ? 1 : n - 1)) % n);
  }, { passive: false });

  function pause() { Game.paused = true; Game.input.down = false; UI.showPause(); }

  window.addEventListener('keydown', ev => {
    Sfx.init();
    const k = ev.key.toLowerCase();
    if (k === 'm' && !ev.repeat) { Sfx.toggleMute(); return; }
    if (Game.state !== 'night') return;
    if (k === 'escape' || k === 'p') { if (Game.paused) { Game.paused = false; UI.hide(); } else pause(); return; }
    if (Game.paused || ev.repeat) return;
    if (k === 'r') pressReload();
    else if (k === '1' || k === '2' || k === '3') switchTo(+k - 1);
    else if (k === 'q') switchTo(Game.N.prev);
    else if (k === ' ') { ev.preventDefault(); useGadget(0); }
    else if (k === 'e') useGadget(1);
  });

  UI.init(document.getElementById('ui'));
  UI.showTitle();

  let last = performance.now();
  function frame(now) {
    let dt = Math.min(0.05, (now - last) / 1000);
    last = now;
    Game.time += dt;
    if (Game.state === 'night' && Game.N && !Game.paused) {
      if (Game.N.slow > 0) { Game.N.slow -= dt; dt *= 0.35; }
      updateNight(dt);
    }
    canvas.style.cursor = (Game.state === 'night' && !Game.paused) || Game.state === 'place' ? 'none' : 'default';
    render(ctx);
    requestAnimationFrame(frame);
  }
  requestAnimationFrame(frame);

  // Debug hooks (docs/TECH.md). Harmless in normal play.
  window.TW = {
    Game,
    give(n = 5000) { if (Game.run) { Game.run.scrap += n; if (Game.state === 'shop') UI.showShop(); } },
    night(n) { if (Game.run) { Game.run.night = n; if (Game.state === 'shop') UI.showShop(); } },
    killAll() { if (Game.N) Game.N.enemies.forEach(e => kill(e, {})); },
    skip() { if (Game.N) { Game.N.qi = Game.N.queue.length; Game.N.enemies.forEach(e => kill(e, {})); } },
    god() { if (Game.run) { Game.run.fort.reinforce = 200; Game.run.wallHp = wallMax(Game.run); } },
  };
  if (/[?&]dev\b/.test(location.search)) {
    const s = document.createElement('script');
    s.src = 'js/devsim.js';
    document.body.appendChild(s);
  }
})();
