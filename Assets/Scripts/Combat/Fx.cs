// TideWall 3D — visual effects. One shared ParticleSystem per effect type; gameplay code calls Emit-style
// helpers (Fx.Drip, Fx.Gore…), so 80 dripping enemies cost one system, not 80. Tracers and light flashes are pooled.
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    public static class Fx
    {
        static ParticleSystem drips, gore, sparks, smoke, fire, splash, ink, bubbles, debris;
        static readonly List<(LineRenderer lr, float t, float max)> tracers = new();
        static readonly List<(Light l, float t, float max, float peak)> flashes = new();
        static Transform root;

        public static void Init(Transform parent)
        {
            // Pools are static, but their objects die with the scene (restart / tests): start clean.
            tracers.Clear();
            flashes.Clear();
            root = new GameObject("Fx").transform;
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
            r.material = additive ? Mats.ParticleAdditive : Mats.Particle;
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
            for (int i = 0; i < n; i++) Emit(gore, p, R(3) + Vector3.up * 2, c, Random.Range(.06f, .16f), Random.Range(.5f, .9f));
        }
        public static void Sparks(Vector3 p, Color c, int n = 6)
        {
            for (int i = 0; i < n; i++) Emit(sparks, p, R(5), c, Random.Range(.04f, .09f), Random.Range(.15f, .35f));
        }
        public static void Smoke(Vector3 p, int n = 6, float spread = .4f, Color? c = null)
        {
            for (int i = 0; i < n; i++) Emit(smoke, p + R(spread), R(.6f) + Vector3.up * .8f, c ?? new Color(.55f, .55f, .58f, .6f), Random.Range(.5f, 1.1f), Random.Range(1.1f, 1.8f));
        }
        public static void Fire(Vector3 p, int n = 3, float spread = .3f)
        {
            for (int i = 0; i < n; i++) Emit(fire, p + R(spread), R(.4f) + Vector3.up * 1.6f, Color.Lerp(new Color(1, .45f, .05f), new Color(1, .85f, .2f), Random.value),
                Random.Range(.15f, .35f), Random.Range(.35f, .6f));
        }
        public static void Splash(Vector3 p, int n = 10)
        {
            for (int i = 0; i < n; i++) Emit(splash, p, new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(2, 4), Random.Range(-1.5f, 1.5f)),
                new Color(.85f, .93f, 1, .9f), Random.Range(.08f, .16f), Random.Range(.5f, .8f));
        }
        public static void Ink(Vector3 p, int n = 14)
        {
            for (int i = 0; i < n; i++) Emit(ink, p + R(.3f), R(1.5f), new Color(.12f, .05f, .15f, .85f), Random.Range(.3f, .7f), Random.Range(1f, 1.6f));
        }
        public static void Bubbles(Vector3 p, int n = 3)
        {
            for (int i = 0; i < n; i++) Emit(bubbles, p + R(.3f), new Vector3(0, .6f, 0) + R(.2f), new Color(.8f, .95f, 1, .7f), Random.Range(.06f, .14f), Random.Range(1, 1.8f));
        }
        public static void Debris(Vector3 p, Color c, int n = 6)
        {
            for (int i = 0; i < n; i++) Emit(debris, p + R(.4f), R(2.5f) + Vector3.up * 2.5f, c, Random.Range(.1f, .22f), Random.Range(.8f, 1.2f));
        }

        public static void Explosion(Vector3 p, float radius)
        {
            for (int i = 0; i < 40; i++) Emit(fire, p + R(radius * .3f), R(radius * 2.2f) + Vector3.up * 2, new Color(1, Random.Range(.4f, .8f), .1f), Random.Range(.4f, .9f), Random.Range(.3f, .6f));
            Smoke(p, 18, radius * .4f, new Color(.3f, .3f, .32f, .7f));
            Sparks(p, new Color(1, .8f, .3f), 20);
            Flash(p + Vector3.up, new Color(1, .7f, .3f), radius * 3, 8, .35f);
        }

        public static void Tracer(Vector3 a, Vector3 b, Color c, float width = .05f, float life = .07f)
        {
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
