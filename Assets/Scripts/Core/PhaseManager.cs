// TideWall 3D — runs the PhaseCycle: enters each phase, blends the lighting, checks its end conditions and
// moves on. It only reads PhaseDefinition data, so phases can be added or removed without touching code.
using System;
using UnityEngine;

namespace TideWall
{
    public class PhaseManager : MonoBehaviour
    {
        public int Day { get; private set; } = 1;
        public int Index { get; private set; } = -1;
        public PhaseDefinition Current { get; private set; }
        public float Elapsed { get; private set; }
        public bool ShopOpen => Current != null && Current.shopOpen;
        public float TimeLeft => Current != null && Current.durationSeconds > 0 ? Mathf.Max(0, Current.durationSeconds - Elapsed) : -1;
        public Color SkyColor { get; private set; } = new(.55f, .75f, .95f);

        public event Action<PhaseDefinition> OnPhaseStart;

        const float BlendSeconds = 4f;
        PhaseDefinition from;
        float blend = 1;
        GameManager gm;

        void Awake() => gm = GetComponent<GameManager>();

        void Start()
        {
            // Lobby: show the first phase's look behind the title.
            if (gm.Cycle.phases.Count > 0) { from = Current = gm.Cycle.phases[0]; ApplyLook(1); }
        }

        public void Begin() => Enter(0);

        /// <summary>Jump straight to a phase (debug hooks and tests).</summary>
        public void Enter(int i)
        {
            var phases = gm.Cycle.phases;
            if (phases.Count == 0) { Debug.LogError("TideWall: the PhaseCycle has no phases"); return; }
            from = Current;
            Index = ((i % phases.Count) + phases.Count) % phases.Count;
            Current = phases[Index];
            Elapsed = 0;
            blend = 0;
            if (!Current.endWhenAllReady && !Current.endWhenWavesCleared && Current.durationSeconds <= 0)
                Debug.LogWarning($"TideWall: phase '{Current.id}' has no end condition; it will end after 60 s.");
            gm.World.OnPhaseStart(Current, gm.Team.ExtraGasCans);
            gm.Waves.StartPhase(Current, Day);
            gm.Players.OnPhaseStart(Current);
            gm.Banner($"{Current.displayName.ToUpperInvariant()} · DAY {Day}", Current.subtitle);
            Sfx.Play(Current.waves > 0 ? "horn" : "bell");
            OnPhaseStart?.Invoke(Current);
        }

        void Update()
        {
            if (Current == null) return;
            if (blend < 1) { blend = Mathf.Min(1, blend + Time.deltaTime / BlendSeconds); ApplyLook(blend); }
            if (gm.State != GameState.Playing) return;
            Elapsed += Time.deltaTime;
            if (ShouldEnd()) Advance();
        }

        bool ShouldEnd()
        {
            var p = Current;
            bool noCondition = !p.endWhenAllReady && !p.endWhenWavesCleared && p.durationSeconds <= 0;
            if (p.durationSeconds > 0 && Elapsed >= p.durationSeconds) return true;
            if (noCondition && Elapsed >= 60) return true;
            if (p.endWhenAllReady && gm.Players.AllReady) return true;
            if (p.endWhenWavesCleared && gm.Waves.Cleared && Elapsed > 1) return true;
            return false;
        }

        public void Advance()
        {
            if (Current.advancesDay) Day++;
            Enter(Index + 1);
        }

        void ApplyLook(float t)
        {
            var a = from ?? Current; var b = Current;
            float k = Mathf.SmoothStep(0, 1, t);
            SkyColor = Color.Lerp(a.skyColor, b.skyColor, k);
            var sun = gm.World.sun;
            sun.color = Color.Lerp(a.sunColor, b.sunColor, k);
            sun.intensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, k);
            sun.transform.rotation = Quaternion.Slerp(Quaternion.Euler(a.sunPitch, a.sunYaw, 0), Quaternion.Euler(b.sunPitch, b.sunYaw, 0), k);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(a.ambientColor, b.ambientColor, k);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Color.Lerp(a.fogColor, b.fogColor, k);
            RenderSettings.fogDensity = Mathf.Lerp(a.fogDensity, b.fogDensity, k);
            gm.World.SetNightLights(Mathf.Lerp(a.torchesLit ? 1 : 0, b.torchesLit ? 1 : 0, k), Mathf.Lerp(a.lanterns ? 1 : 0, b.lanterns ? 1 : 0, k));
        }

        public bool LanternsOn => Current != null && Current.lanterns;
    }
}
