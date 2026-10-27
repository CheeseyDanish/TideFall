// TideWall 3D — what every playable scene's level has in common, whatever part of the campaign it belongs to:
// the sun and the sky, torches and player lanterns, drifting clouds, where players spawn, where creatures go when no
// player is near, where defences may be built, and any extra state an online host has to send.
//   World       Part I: the town, the wall and the beach (defend)
//   Expedition  Part II: a stage on the drained seabed (attack)
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TideWall
{
    /// <summary>Where a creature is heading when no player is close enough to fight.</summary>
    public enum GoalKind { Wall, Breach, Plant, Hunt, Home }

    public abstract class Level : MonoBehaviour
    {
        [Serializable] public struct Cloud { public Transform t; public float speed; }

        public Light sun;
        public List<Light> torches = new();
        public List<Cloud> clouds = new();
        [NonSerialized] public readonly List<Light> lanterns = new();       // player lanterns register here

        protected float torchLevel, lanternLevel;
        Material sky;

        /// <summary>A fresh run: hit points, lines and anything else that comes from the data.</summary>
        public virtual void Init(GameDatabase db) { }

        /// <summary>A phase began (Part I's mornings put food and gas cans on the beach).</summary>
        public virtual void OnPhaseStart(PhaseDefinition p, int extraGasCans) { }

        /// <summary>Where creatures go when no player is in reach.</summary>
        public abstract Vector3 Goal(Vector3 from, float radius, out GoalKind kind);

        /// <summary>What a flying creature dives at when it has no player to chase.</summary>
        public abstract Vector3 FlyerTarget(Vector3 from);

        /// <summary>Where player `index` starts and respawns.</summary>
        public abstract Vector3 SpawnPoint(int index);

        /// <summary>Why a defence can't go here, or null if it can (on top of the rules every level shares).</summary>
        public abstract string BuildRule(Vector3 spot, float radius);

        /// <summary>The second HUD line under the phase (Part I: the powerplant and the wall; Part II: the objective).</summary>
        public abstract string StatusLine();

        /// <summary>0..1 for the bar under the phase line.</summary>
        public abstract float StatusFraction();

        /// <summary>Online: anything about the level the host's snapshots carry beyond the shared state.</summary>
        public virtual void WriteNet(BinaryWriter w) { }
        public virtual void ReadNet(BinaryReader r) { }

        public void SetNightLights(float torches01, float lanterns01)
        {
            torchLevel = torches01; lanternLevel = lanterns01;
            OnNightLights(torches01);
        }

        protected virtual void OnNightLights(float torches01) { }

        /// <summary>The procedural sky follows the phase: tint from its sky colour, brightness from its sun.</summary>
        public void SetSky(Color tint, float sunIntensity)
        {
            if (sky == null)
            {
                var baseSky = Content.Material("Sky");
                if (baseSky == null) return;
                sky = new Material(baseSky) { name = "Sky (runtime)" };
                RenderSettings.skybox = sky;
                RenderSettings.sun = sun;
            }
            float day = Mathf.Clamp01(sunIntensity / 1.3f);
            sky.SetColor("_SkyTint", Color.Lerp(new Color(.5f, .5f, .5f), tint, .4f));
            sky.SetColor("_GroundColor", Color.Lerp(new Color(.05f, .07f, .14f), new Color(.45f, .55f, .62f), day));
            sky.SetFloat("_Exposure", Mathf.Lerp(.18f, 1.1f, day));
            sky.SetFloat("_AtmosphereThickness", Mathf.Lerp(.5f, .85f, day));
            sky.SetFloat("_SunSize", .045f);
        }

        /// <summary>Every frame for every camera: the sky (when there is one; else the phase colour) and the graphics settings.</summary>
        public static void SetupCameraSky(Camera c, Color fallback)
        {
            GraphicsApply.ConfigureCamera(c);   // anti-aliasing, post-processing, draw distance from Settings
            if (RenderSettings.skybox != null) c.clearFlags = CameraClearFlags.Skybox;
            else { c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = fallback; }
        }

        protected virtual void Update()
        {
            float t = Time.time;
            for (int i = 0; i < torches.Count; i++)
            {
                var l = torches[i];
                if (!l) continue;
                l.intensity = torchLevel * (6f + Mathf.PerlinNoise(t * 3, i * 7.3f) * 3f);
                l.enabled = torchLevel > .01f;
            }
            foreach (var l in lanterns) if (l) { l.intensity = lanternLevel * 4f; l.enabled = lanternLevel > .01f; }
            foreach (var c in clouds)
            {
                if (!c.t) continue;
                var p = c.t.position;
                p.x += c.speed * Time.deltaTime;
                if (p.x > 220) p.x = -220;
                c.t.position = p;
            }
        }
    }
}
