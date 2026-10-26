// TideWall 3D — visual effects. One shared ParticleSystem per effect type; gameplay code calls Emit-style
// helpers (Fx.Drip, Fx.Gore…), so 80 dripping enemies cost one system, not 80. Tracers and light flashes are pooled.
// On an online host every effect except drips is also sent to the clients (drips are made locally from wetness).
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    public enum FxKind : byte { Gore, Sparks, Smoke, Fire, Splash, Ink, Bubbles, Debris, Explosion, Tracer, Flash }

    public static class Fx
    {
        static int quiet;   // > 0 while an effect is made of other effects (an explosion): only the outer one is sent

        static void Relay(FxKind k, Vector3 a, Vector3 b, Color c, float f1 = 0, float f2 = 0, float f3 = 0, int n = 0)
        {
            if (quiet == 0 && Net.IsHost) Net.Host.Fx(new FxEvent { kind = k, a = a, b = b, color = c, f1 = f1, f2 = f2, f3 = f3, n = n });
        }

        /// <summary>Client: replay an effect the host made.</summary>
        public static void Play(FxEvent e)
        {
            switch (e.kind)
            {
                case FxKind.Gore: Gore(e.a, e.color, e.n); break;
                case FxKind.Sparks: Sparks(e.a, e.color, e.n); break;
                case FxKind.Smoke: Smoke(e.a, e.n, e.f1, e.color); break;
                case FxKind.Fire: Fire(e.a, e.n, e.f1); break;
                case FxKind.Splash: Splash(e.a, e.n); break;
                case FxKind.Ink: Ink(e.a, e.n); break;
                case FxKind.Bubbles: Bubbles(e.a, e.n); break;
                case FxKind.Debris: Debris(e.a, e.color, e.n); break;
                case FxKind.Explosion: Explosion(e.a, e.f1); break;
                case FxKind.Tracer: Tracer(e.a, e.b, e.color, e.f1, e.f2); break;
                case FxKind.Flash: Flash(e.a, e.color, e.f1, e.f2, e.f3); break;
            }
        }

        static ParticleSystem drips, gore, sparks, smoke, fire, splash, ink, bubbles, debris;
        static readonly List<(LineRenderer lr, float t, float max)> tracers = new();
        static readonly List<(Light l, float t, float max, float peak)> flashes = new();
        static Transform root;
        static Material soft, softAdditive;   // particle materials with a round, soft-edged sprite

        public static void Init(Transform parent)
        {
            // Pools are static, but their objects die with the scene (restart / tests): start clean.
            tracers.Clear();
            flashes.Clear();
            root = new GameObject("Fx").transform;
            if (soft == null)
            {
                var tex = Ui.Radial(false, 64).texture;
                soft = new Material(Mats.Particle) { name = "FxSoft" };
                softAdditive = new Material(Mats.ParticleAdditive) { name = "FxSoftAdditive" };
                foreach (var m in new[] { soft, softAdditive }) { m.mainTexture = tex; if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex); }
            }
            root.SetParent(parent, false);
            drips = Make("Drips", .9f, 400, false, .6f);
            gore = Make("Gore", 1.6f, 500, false, .9f);
            sparks = Make("Sparks", .6f, 300, true, .35f);
            smoke = Make("Smoke", -.08f, 300, false, 1.6f, grow: true);
            fire = Make("Fire", -.25f, 400, true, .6f, grow: false);
            splash = Make("Splash", 1.2f, 300, false, .7f);
            ink = Make("Ink", .5f, 200, false, 1.4f, grow: true);
            bubbles = Make("Bubbles", -.3f, 200, false, 1.5f);
            debris = Make("Debris", 1.8f, 200, false, 1.2f);
        }

        static ParticleSystem Make(string name, float gravity, int max, bool additive, float life, bool grow = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false; main.loop = true; main.duration = 10;
            main.maxParticles = max; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity; main.startLifetime = life;
            var em = ps.emission; em.enabled = false;
            var sh = ps.shape; sh.enabled = false;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.8f, .6f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, grow ? AnimationCurve.Linear(0, .6f, 1, 1.6f) : AnimationCurve.Linear(0, 1, 1, .3f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = additive ? softAdditive : soft;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        static void Emit(ParticleSystem ps, Vector3 p, Vector3 v, Color c, float size, float life)
        {
            if (ps == null) return;
            ps.Emit(new ParticleSystem.EmitParams { position = p, velocity = v, startColor = c, startSize = size, startLifetime = life }, 1);
        }

        static Vector3 R(float s) => Random.insideUnitSphere * s;

        /// <summary>One water drop falling off a wet creature. Called more often the wetter it is.</summary>
        public static void Drip(Vector3 p) => Emit(drips, p, new Vector3(Random.Range(-.2f, .2f), -.4f, Random.Range(-.2f, .2f)),
            new Color(.6f, .8f, 1f, .9f), Random.Range(.04f, .08f), Random.Range(.5f, .8f));

        public static void Gore(Vector3 p, Color c, int n = 10)
        {
            Relay(FxKind.Gore, p, default, c, n: n);
            for (int i = 0; i < n; i++) Emit(gore, p, R(3) + Vector3.up * 2, c, Random.Range(.06f, .16f), Random.Range(.5f, .9f));
        }
        public static void Sparks(Vector3 p, Color c, int n = 6)
        {
            Relay(FxKind.Sparks, p, default, c, n: n);
            for (int i = 0; i < n; i++) Emit(sparks, p, R(5), c, Random.Range(.04f, .09f), Random.Range(.15f, .35f));
        }
        public static void Smoke(Vector3 p, int n = 6, float spread = .4f, Color? c = null)
        {
            Relay(FxKind.Smoke, p, default, c ?? new Color(.55f, .55f, .58f, .6f), spread, n: n);
            for (int i = 0; i < n; i++) Emit(smoke, p + R(spread), R(.6f) + Vector3.up * .8f, c ?? new Color(.55f, .55f, .58f, .6f), Random.Range(.5f, 1.1f), Random.Range(1.1f, 1.8f));
        }
        /// <summary>Steam from the powerplant's cooling towers. Not sent online: every machine makes its own scenery.</summary>
        public static void Steam(Vector3 p, float danger)
        {
            var c = Color.Lerp(new Color(.95f, .97f, 1f, .5f), new Color(.6f, .95f, .55f, .6f), danger);
            for (int i = 0; i < 2; i++) Emit(smoke, p + R(.6f), R(.3f) + Vector3.up * (1.6f + danger * 1.5f), c, Random.Range(1.2f, 2.2f), Random.Range(2.2f, 3.2f));
        }

        public static void Fire(Vector3 p, int n = 3, float spread = .3f)
        {
            Relay(FxKind.Fire, p, default, default, spread, n: n);
            for (int i = 0; i < n; i++) Emit(fire, p + R(spread), R(.4f) + Vector3.up * 1.6f, Color.Lerp(new Color(1, .45f, .05f), new Color(1, .85f, .2f), Random.value),
                Random.Range(.15f, .35f), Random.Range(.35f, .6f));
        }
        public static void Splash(Vector3 p, int n = 10)
        {
            Relay(FxKind.Splash, p, default, default, n: n);
            for (int i = 0; i < n; i++) Emit(splash, p, new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(2, 4), Random.Range(-1.5f, 1.5f)),
                new Color(.85f, .93f, 1, .9f), Random.Range(.08f, .16f), Random.Range(.5f, .8f));
        }
        public static void Ink(Vector3 p, int n = 14)
        {
            Relay(FxKind.Ink, p, default, default, n: n);
            for (int i = 0; i < n; i++) Emit(ink, p + R(.3f), R(1.5f), new Color(.12f, .05f, .15f, .85f), Random.Range(.3f, .7f), Random.Range(1f, 1.6f));
        }
        public static void Bubbles(Vector3 p, int n = 3)
        {
            Relay(FxKind.Bubbles, p, default, default, n: n);
            for (int i = 0; i < n; i++) Emit(bubbles, p + R(.3f), new Vector3(0, .6f, 0) + R(.2f), new Color(.8f, .95f, 1, .7f), Random.Range(.06f, .14f), Random.Range(1, 1.8f));
        }
        public static void Debris(Vector3 p, Color c, int n = 6)
        {
            Relay(FxKind.Debris, p, default, c, n: n);
            for (int i = 0; i < n; i++) Emit(debris, p + R(.4f), R(2.5f) + Vector3.up * 2.5f, c, Random.Range(.1f, .22f), Random.Range(.8f, 1.2f));
        }

        public static void Explosion(Vector3 p, float radius)
        {
            Relay(FxKind.Explosion, p, default, default, radius);
            quiet++;
            for (int i = 0; i < 40; i++) Emit(fire, p + R(radius * .3f), R(radius * 2.2f) + Vector3.up * 2, new Color(1, Random.Range(.4f, .8f), .1f), Random.Range(.4f, .9f), Random.Range(.3f, .6f));
            Smoke(p, 18, radius * .4f, new Color(.3f, .3f, .32f, .7f));
            Sparks(p, new Color(1, .8f, .3f), 20);
            Flash(p + Vector3.up, new Color(1, .7f, .3f), radius * 3, 8, .35f);
            quiet--;
        }

        public static void Tracer(Vector3 a, Vector3 b, Color c, float width = .05f, float life = .07f)
        {
            Relay(FxKind.Tracer, a, b, c, width, life);
            LineRenderer lr = null;
            for (int i = 0; i < tracers.Count; i++) if (!tracers[i].lr.enabled) { lr = tracers[i].lr; tracers.RemoveAt(i); break; }
            if (lr == null)
            {
                var go = new GameObject("Tracer");
                go.transform.SetParent(root, false);
                lr = go.AddComponent<LineRenderer>();
                lr.material = Mats.ParticleAdditive; lr.positionCount = 2; lr.numCapVertices = 2;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            lr.enabled = true;
            lr.SetPosition(0, a); lr.SetPosition(1, b);
            lr.startWidth = width; lr.endWidth = width * .5f;
            lr.startColor = c; lr.endColor = new Color(c.r, c.g, c.b, 0);
            tracers.Add((lr, life, life));
        }

        public static void Flash(Vector3 p, Color c, float range, float intensity, float life)
        {
            Relay(FxKind.Flash, p, default, c, range, intensity, life);
            Light l = null;
            for (int i = 0; i < flashes.Count; i++) if (!flashes[i].l.enabled) { l = flashes[i].l; flashes.RemoveAt(i); break; }
            if (l == null)
            {
                if (flashes.Count > 12) return;
                l = new GameObject("Flash").AddComponent<Light>();
                l.transform.SetParent(root, false);
                l.type = LightType.Point; l.shadows = LightShadows.None;
            }
            l.enabled = true; l.transform.position = p; l.color = c; l.range = range; l.intensity = intensity;
            flashes.Add((l, life, life, intensity));
        }

        public static void Tick(float dt)
        {
            tracers.RemoveAll(t => t.lr == null);
            flashes.RemoveAll(f => f.l == null);
            for (int i = 0; i < tracers.Count; i++)
            {
                var (lr, t, max) = tracers[i];
                if (!lr.enabled) continue;
                t -= dt;
                if (t <= 0) lr.enabled = false;
                else
                {
                    var c = lr.startColor; c.a = t / max; lr.startColor = c;
                }
                tracers[i] = (lr, t, max);
            }
            for (int i = 0; i < flashes.Count; i++)
            {
                var (l, t, max, peak) = flashes[i];
                if (!l.enabled) continue;
                t -= dt;
                if (t <= 0) l.enabled = false; else l.intensity = peak * t / max;
                flashes[i] = (l, t, max, peak);
            }
        }
    }
}
