// Tidewall — the shop backdrop: daytime, seen from inside the castle through a wide window.
// On the sand: umbrellas, towels and a beach-volleyball game. Under the water (a cut-away view):
// the creatures from the night, huddled around a battle plan. Drawn behind the shop UI; the shop's
// see-through .vista band (ui.js / style.css) lines up with the window (WIN below).
'use strict';

const ShopScene = (() => {
  // Window opening in canvas coordinates. The shop UI is scaled to 0.82, so its .vista band
  // (268 CSS px under a ~58 px header) covers roughly y 48–268 on the canvas.
  const WIN = { x0: 70, x1: 1210, y0: 56, y1: 262, r: 46 };
  const SEA_Y = 146;        // sea surface inside the window (high, so the underwater cut-away has room)
  const SAND_Y = 164;       // beach surface
  const CLOUDS = Array.from({ length: 6 }, (_, i) => ({ x: i * 230 + Math.random() * 120, y: 70 + Math.random() * 30, s: 0.7 + Math.random() * 0.6, v: 6 + Math.random() * 8 }));
  const BUBBLES = Array.from({ length: 26 }, () => ({ x: 820 + Math.random() * 340, p: Math.random(), v: 0.15 + Math.random() * 0.25, r: 1 + Math.random() * 2.2 }));

  function windowPath(ctx) {
    const { x0, x1, y0, y1, r } = WIN;
    ctx.beginPath();
    ctx.moveTo(x0, y1); ctx.lineTo(x0, y0 + r); ctx.quadraticCurveTo(x0, y0, x0 + r, y0);
    ctx.lineTo(x1 - r, y0); ctx.quadraticCurveTo(x1, y0, x1, y0 + r); ctx.lineTo(x1, y1); ctx.closePath();
  }

  function stoneWall(ctx) {
    ctx.fillStyle = '#3a322b'; ctx.fillRect(0, 0, W, H);
    const bw = 64, bh = 30;
    for (let row = 0, y = 0; y < H; row++, y += bh) {
      for (let x = -(row % 2) * bw / 2; x < W; x += bw) {
        const shade = 0.9 + ((row * 13 + Math.round(x / bw) * 7) % 5) * 0.04;
        ctx.fillStyle = rgb([82, 71, 60], shade);
        ctx.fillRect(x + 2, y + 2, bw - 4, bh - 4);
      }
    }
    // warm torchlight from both sides
    for (const tx of [24, W - 24]) {
      const g = ctx.createRadialGradient(tx, 180, 10, tx, 180, 260);
      g.addColorStop(0, 'rgba(251,191,36,0.28)'); g.addColorStop(1, 'rgba(251,191,36,0)');
      ctx.fillStyle = g; ctx.fillRect(0, 0, W, H);
    }
  }

  function sky(ctx, t) {
    const g = ctx.createLinearGradient(0, WIN.y0, 0, SEA_Y);
    g.addColorStop(0, '#5fb3ea'); g.addColorStop(1, '#cdeefc');
    ctx.fillStyle = g; ctx.fillRect(WIN.x0, WIN.y0, WIN.x1 - WIN.x0, SEA_Y - WIN.y0);
    const sg = ctx.createRadialGradient(1090, 92, 10, 1090, 92, 110);
    sg.addColorStop(0, 'rgba(255,250,220,0.95)'); sg.addColorStop(0.25, 'rgba(255,240,180,0.5)'); sg.addColorStop(1, 'rgba(255,240,180,0)');
    ctx.fillStyle = sg; ctx.fillRect(960, WIN.y0, 250, 160);
    ctx.fillStyle = '#fff8d6'; ctx.beginPath(); ctx.arc(1090, 92, 24, 0, Math.PI * 2); ctx.fill();
    ctx.fillStyle = 'rgba(255,255,255,0.9)';
    for (const c of CLOUDS) {
      const x = WIN.x0 + ((c.x + t * c.v) % (WIN.x1 - WIN.x0 + 200)) - 100;
      for (const [dx, dy, rr] of [[0, 0, 18], [20, -8, 22], [42, 0, 17], [20, 6, 16]]) {
        ctx.beginPath(); ctx.arc(x + dx * c.s, c.y + dy * c.s, rr * c.s, 0, Math.PI * 2); ctx.fill();
      }
    }
    // distant gulls
    ctx.strokeStyle = 'rgba(30,41,59,0.6)'; ctx.lineWidth = 1.5;
    for (let i = 0; i < 3; i++) {
      const x = 640 + i * 70 + Math.sin(t * 0.4 + i) * 30, y = 100 + i * 12 + Math.sin(t * 2 + i) * 3, f = Math.sin(t * 6 + i) * 3;
      ctx.beginPath(); ctx.moveTo(x - 7, y - f); ctx.quadraticCurveTo(x - 3, y - 4, x, y); ctx.quadraticCurveTo(x + 3, y - 4, x + 7, y - f); ctx.stroke();
    }
  }

  function sea(ctx, t) {
    // cut-away: surface on top, depth below
    const g = ctx.createLinearGradient(0, SEA_Y, 0, WIN.y1);
    g.addColorStop(0, '#3fa7d0'); g.addColorStop(1, '#173f73');
    ctx.fillStyle = g; ctx.fillRect(WIN.x0, SEA_Y, WIN.x1 - WIN.x0, WIN.y1 - SEA_Y);
    // light shafts
    ctx.fillStyle = 'rgba(255,255,255,0.06)';
    for (let i = 0; i < 5; i++) {
      const x = 760 + i * 95 + Math.sin(t * 0.5 + i) * 10;
      ctx.beginPath(); ctx.moveTo(x, SEA_Y); ctx.lineTo(x + 26, SEA_Y); ctx.lineTo(x + 60, WIN.y1); ctx.lineTo(x + 20, WIN.y1); ctx.fill();
    }
    // seabed
    ctx.fillStyle = '#6b5a3c';
    ctx.beginPath(); ctx.moveTo(640, WIN.y1); ctx.quadraticCurveTo(760, 246, 900, 252); ctx.lineTo(WIN.x1, 250); ctx.lineTo(WIN.x1, WIN.y1); ctx.fill();
    // seaweed
    ctx.strokeStyle = '#2f6b3a'; ctx.lineWidth = 3;
    for (const sx of [770, 815, 1150, 1185]) {
      ctx.beginPath(); ctx.moveTo(sx, 254);
      for (let k = 1; k <= 4; k++) ctx.lineTo(sx + Math.sin(t * 1.5 + k + sx) * 5, 254 - k * 9);
      ctx.stroke();
    }
    // a little sailboat on the horizon
    const bx = 720 + Math.sin(t * 0.15) * 30;
    ctx.fillStyle = '#7c4a26'; ctx.fillRect(bx - 14, SEA_Y - 5, 28, 5);
    ctx.fillStyle = '#f8fafc'; ctx.beginPath(); ctx.moveTo(bx, SEA_Y - 32); ctx.lineTo(bx, SEA_Y - 6); ctx.lineTo(bx + 16, SEA_Y - 6); ctx.fill();
  }

  // Draw one enemy with its battle painter at a given foot point and scale
  function mini(ctx, id, x, footY, s, t, opts = {}) {
    const d = ENEMIES[id];
    const e = { id, d, x, y: 0, w: d.w, h: d.h, lift: 0, anim: t * (opts.speed || 0.6) + x * 0.01, attacking: !!opts.attacking,
      swing: opts.swing || 0, singT: 0, emerge: 0, state: opts.state || 'fly' };
    const c = hexRgb(d.color).slice();
    mixInto(c, '#1e3a8a', 0.3);     // underwater tint
    ctx.save();
    ctx.translate(x, footY); ctx.scale(s, s); ctx.translate(-x, 0);
    DRAW[d.boss || id](ctx, e, boxOf(e), c, Math.sin(e.anim * 4) * 0.4, t);
    ctx.restore();
  }

  function plotters(ctx, t) {
    // rock table with the battle plan: a sketch of the wall with an X on it
    ctx.fillStyle = '#4b5563'; ctx.beginPath(); ctx.ellipse(990, 246, 54, 12, 0, 0, Math.PI * 2); ctx.fill();
    ctx.fillStyle = '#374151'; ctx.fillRect(972, 246, 36, 10);
    ctx.fillStyle = '#e7d9b4'; ctx.fillRect(954, 228, 72, 16);
    ctx.fillStyle = '#6b5b45'; ctx.fillRect(961, 232, 10, 10); for (let i = 0; i < 3; i++) ctx.fillRect(961 + i * 4, 229, 2, 3);
    ctx.strokeStyle = '#b91c1c'; ctx.lineWidth = 2;
    ctx.beginPath(); ctx.moveTo(979, 231); ctx.lineTo(987, 239); ctx.moveTo(987, 231); ctx.lineTo(979, 239); ctx.stroke();
    ctx.strokeStyle = '#6b5b45'; ctx.setLineDash([3, 2]); ctx.beginPath(); ctx.moveTo(1020, 236); ctx.lineTo(991, 236); ctx.stroke(); ctx.setLineDash([]);

    mini(ctx, 'crab', 915, 256, 1.25, t, { speed: 1.2 });
    mini(ctx, 'shellback', 1110, 256, 0.85, t, { speed: 0.4 });
    mini(ctx, 'drowner', 1048, 256, 1.05, t, { attacking: true, speed: 0.9 });       // jabbing at the plan
    mini(ctx, 'puffer', 990 + Math.sin(t * 0.8) * 6, 206 + Math.sin(t * 1.3) * 3, 0.85, t, { speed: 0.5 });
    mini(ctx, 'eel', 860 + Math.sin(t * 0.6) * 14, 196 + Math.sin(t) * 4, 1.0, t, { speed: 0.7 });
    // a thought bubble over the drowner: they're plotting
    const bx = 1072, by = 170 + Math.sin(t * 2) * 2;
    ctx.fillStyle = 'rgba(255,255,255,0.85)';
    ctx.beginPath(); ctx.arc(1056, by + 26, 2.5, 0, Math.PI * 2); ctx.arc(1062, by + 17, 3.5, 0, Math.PI * 2); ctx.fill();
    ctx.beginPath(); ctx.ellipse(bx, by, 18, 11, 0, 0, Math.PI * 2); ctx.fill();
    ctx.fillStyle = '#57534e'; ctx.fillRect(bx - 10, by - 4, 6, 8);
    ctx.fillStyle = '#b91c1c'; ctx.font = '700 11px sans-serif'; ctx.textAlign = 'center'; ctx.fillText('!!', bx + 5, by + 4);
    // bubbles
    for (const b of BUBBLES) {
      const p = (b.p + t * b.v) % 1, y = 252 - p * (252 - SEA_Y);
      ctx.strokeStyle = `rgba(224,242,254,${0.7 * (1 - p)})`; ctx.lineWidth = 1;
      ctx.beginPath(); ctx.arc(b.x + Math.sin(t * 2 + b.x) * 3, y, b.r, 0, Math.PI * 2); ctx.stroke();
    }
  }

  function beach(ctx, t) {
    // sand, sloping into the water
    ctx.fillStyle = '#e9cf98';
    ctx.beginPath(); ctx.moveTo(WIN.x0, SAND_Y); ctx.lineTo(560, SAND_Y); ctx.quadraticCurveTo(640, SAND_Y + 6, 700, WIN.y1); ctx.lineTo(WIN.x0, WIN.y1); ctx.fill();
    ctx.fillStyle = 'rgba(160,120,60,0.18)';
    for (let i = 0; i < 40; i++) ctx.fillRect(WIN.x0 + (i * 53) % 560, SAND_Y + 8 + (i * 29) % 50, 3, 1.5);
    // surf foam where sand meets sea
    ctx.strokeStyle = 'rgba(255,255,255,0.85)'; ctx.lineWidth = 2;
    ctx.beginPath();
    for (let x = 560; x <= WIN.x1; x += 12) ctx.lineTo(x, SEA_Y + Math.sin(x * 0.05 + t * 2) * 2);
    ctx.stroke();

    // palm
    ctx.strokeStyle = '#8b5e34'; ctx.lineWidth = 6;
    const top = 92;
    ctx.beginPath(); ctx.moveTo(105, SAND_Y + 4); ctx.quadraticCurveTo(95, 125, 118, top); ctx.stroke();
    ctx.fillStyle = '#2f855a';
    for (let i = 0; i < 5; i++) {
      const a = -2.6 + i * 0.55 + Math.sin(t + i) * 0.05;
      ctx.beginPath(); ctx.moveTo(118, top);
      ctx.quadraticCurveTo(118 + Math.cos(a) * 30, top + Math.sin(a) * 30 - 10, 118 + Math.cos(a) * 52, top + Math.sin(a) * 52 + 8);
      ctx.quadraticCurveTo(118 + Math.cos(a) * 28, top + Math.sin(a) * 28, 118, top); ctx.fill();
    }

    // umbrellas + towels
    const umbrellas = [[170, ['#dc2626', '#fef2f2']], [255, ['#2563eb', '#fde047']], [540, ['#16a34a', '#f0fdf4']]];
    for (const [x, cols] of umbrellas) {
      ctx.fillStyle = cols[0] === '#2563eb' ? '#f472b6' : '#38bdf8';
      ctx.fillRect(x - 6, SAND_Y + 6, 30, 7);
      ctx.strokeStyle = '#78716c'; ctx.lineWidth = 2; ctx.beginPath(); ctx.moveTo(x, SAND_Y + 8); ctx.lineTo(x - 4, SAND_Y - 46); ctx.stroke();
      for (let k = 0; k < 6; k++) {
        ctx.fillStyle = cols[k % 2];
        ctx.beginPath(); ctx.moveTo(x - 4, SAND_Y - 50);
        ctx.arc(x - 4, SAND_Y - 46, 32, Math.PI + k * Math.PI / 6, Math.PI + (k + 1) * Math.PI / 6); ctx.fill();
      }
    }

    // beach volleyball: net at 410, two players a side, ball arcing back and forth
    const net = 410, base = SAND_Y + 2;
    ctx.strokeStyle = '#57534e'; ctx.lineWidth = 2;
    ctx.beginPath(); ctx.moveTo(net, base); ctx.lineTo(net, base - 40); ctx.stroke();
    ctx.strokeStyle = 'rgba(255,255,255,0.8)'; ctx.lineWidth = 1;
    for (let k = 0; k < 4; k++) { ctx.beginPath(); ctx.moveTo(net - 1, base - 40 + k * 4); ctx.lineTo(net + 1, base - 40 + k * 4); ctx.stroke(); }
    ctx.fillStyle = 'rgba(255,255,255,0.7)'; ctx.fillRect(net - 1, base - 40, 2, 14);
    const period = 1.6, ph = (t % (period * 2)) / period;     // 0..2: left→right, then right→left
    const toRight = ph < 1, s = toRight ? ph : ph - 1;
    const fromX = toRight ? 352 : 470, toX = toRight ? 470 : 352;
    const ballX = fromX + (toX - fromX) * s, ballY = base - 46 - Math.sin(Math.PI * s) * 44;
    const players = [[335, '#f97316', 0], [372, '#a855f7', 0], [450, '#0ea5e9', 1], [488, '#ef4444', 1]];
    for (const [px, coat, side] of players) {
      // the hitter on each side jumps as the ball arrives
      const hitter = (side === 1 && toRight && Math.abs(px - 470) < 20) || (side === 0 && !toRight && Math.abs(px - 352) < 20);
      const jump = hitter ? Math.max(0, Math.sin(Math.PI * Math.min(1, Math.max(0, (s - 0.7) / 0.3)))) * 12 : Math.abs(Math.sin(t * 3 + px)) * 1.5;
      drawPerson(ctx, px, base - jump, coat, 0.7, t);
      // arms up
      ctx.strokeStyle = '#e7c9a9'; ctx.lineWidth = 2.5;
      ctx.beginPath(); ctx.moveTo(px - 4, base - jump - 26); ctx.lineTo(px - 7, base - jump - 42); ctx.moveTo(px + 4, base - jump - 26); ctx.lineTo(px + 7, base - jump - 42); ctx.stroke();
    }
    ctx.fillStyle = '#fefce8'; ctx.beginPath(); ctx.arc(ballX, ballY, 4.5, 0, Math.PI * 2); ctx.fill();
    ctx.strokeStyle = '#eab308'; ctx.lineWidth = 1; ctx.beginPath(); ctx.arc(ballX, ballY, 4.5, t * 4, t * 4 + 2); ctx.stroke();
    ctx.fillStyle = 'rgba(0,0,0,0.12)'; ctx.beginPath(); ctx.ellipse(ballX, base + 2, 5, 1.5, 0, 0, Math.PI * 2); ctx.fill();
  }

  function frame(ctx) {
    // stone reveal around the opening, plus a sill
    windowPath(ctx);
    ctx.strokeStyle = '#6b6158'; ctx.lineWidth = 16; ctx.stroke();
    windowPath(ctx);
    ctx.strokeStyle = '#2b2520'; ctx.lineWidth = 3; ctx.stroke();
    ctx.fillStyle = '#5b5148'; ctx.fillRect(WIN.x0 - 18, WIN.y1, WIN.x1 - WIN.x0 + 36, 14);
    ctx.fillStyle = 'rgba(0,0,0,0.25)'; ctx.fillRect(WIN.x0 - 18, WIN.y1 + 14, WIN.x1 - WIN.x0 + 36, 4);
    // banners hanging either side
    for (const bx of [28, W - 46]) {
      ctx.fillStyle = '#1e3a8a'; ctx.beginPath(); ctx.moveTo(bx, 70); ctx.lineTo(bx + 18, 70); ctx.lineTo(bx + 18, 200); ctx.lineTo(bx + 9, 188); ctx.lineTo(bx, 200); ctx.fill();
      ctx.fillStyle = '#fbbf24'; ctx.fillRect(bx + 4, 96, 10, 10);
    }
  }

  function draw(ctx, t) {
    stoneWall(ctx);
    ctx.save();
    windowPath(ctx); ctx.clip();
    sky(ctx, t);
    sea(ctx, t);
    plotters(ctx, t);
    beach(ctx, t);
    ctx.restore();
    frame(ctx);
  }

  return { draw };
})();
