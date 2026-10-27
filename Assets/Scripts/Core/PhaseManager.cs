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
        float baseFog, cardFog;
        float blend = 1;
        GameManager gm;

        void Awake() => gm = GetComponent<GameManager>();

        /// <summary>Part II: one fixed look for the whole stage; nothing cycles or ends by itself.</summary>
        public bool Holding { get; private set; }

        void Start()
        {
            if (gm.Assault) { Hold(gm.Expedition.Stage.look); return; }
            // Lobby: show the first phase's look behind the title.
            if (gm.Cycle.phases.Count > 0) { from = Current = gm.Cycle.phases[0]; ApplyLook(1); }
        }

        /// <summary>Show this look and stay in it (Part II stages).</summary>
        public void Hold(PhaseDefinition look)
        {
            Holding = true;
            from = Current = look;
            Index = -1;
            ApplyLook(1);
            gm.Players.OnPhaseStart(Current);
        }

        public void Begin() => Enter(0);

        /// <summary>Loading a save: the day it was saved on.</summary>
        public void SetDay(int day) => Day = Mathf.Max(1, day);

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
            gm.Level.OnPhaseStart(Current, gm.Team.ExtraGasCans);
            gm.Waves.StartPhase(Current, Day);
            gm.Players.OnPhaseStart(Current);
            gm.Banner($"{Current.displayName.ToUpperInvariant()} · DAY {Day}", Current.subtitle);
            Sfx.Play(Current.waves > 0 ? "horn" : "bell");
            if (Current.tidalDraft) gm.Cards.OfferDraft();
            if (Current.saveOnStart && gm.SaveNow()) gm.Banner($"{Current.displayName.ToUpperInvariant()} · DAY {Day}", $"{Current.subtitle}\n<size=14>Game saved (slot {gm.SaveSlot})</size>");
            OnPhaseStart?.Invoke(Current);
        }

        /// <summary>Client: follow the host's phase. Only the look changes here; spawns and banners come from the host.</summary>
        public void NetFollow(int index, int day, float elapsed)
        {
            Day = day; Elapsed = elapsed;
            if (Holding || index == Index || gm.Cycle.phases.Count == 0) return;
            from = Current;
            Index = ((index % gm.Cycle.phases.Count) + gm.Cycle.phases.Count) % gm.Cycle.phases.Count;
            Current = gm.Cycle.phases[Index];
            blend = 0;
            gm.Players.OnPhaseStart(Current);
            OnPhaseStart?.Invoke(Current);
        }

        void Update()
        {
            if (Current == null) return;
            if (blend < 1) { blend = Mathf.Min(1, blend + Time.deltaTime / BlendSeconds); ApplyLook(blend); }
            // Tidal cards can thicken the fog (Fog Bank, Downpour)
            cardFog = Mathf.MoveTowards(cardFog, gm.Cards.Fog, Time.deltaTime * .01f);
            RenderSettings.fogDensity = baseFog + cardFog;
            if (gm.State != GameState.Playing || Net.IsClient || Holding) return;
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
            if (Current.waves > 0) gm.Cards.WaveFinished();   // the phase's last wave is over
            if (Current.advancesDay) Day++;
            Enter(Index + 1);
        }

        void ApplyLook(float t)
        {
            var a = from ?? Current; var b = Current;
            float k = Mathf.SmoothStep(0, 1, t);
            SkyColor = Color.Lerp(a.skyColor, b.skyColor, k);
            var sun = gm.Level.sun;
            sun.color = Color.Lerp(a.sunColor, b.sunColor, k);
            sun.intensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, k);
            sun.transform.rotation = Quaternion.Slerp(Quaternion.Euler(a.sunPitch, a.sunYaw, 0), Quaternion.Euler(b.sunPitch, b.sunYaw, 0), k);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(a.ambientColor, b.ambientColor, k);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Color.Lerp(a.fogColor, b.fogColor, k);
            baseFog = Mathf.Lerp(a.fogDensity, b.fogDensity, k);
            RenderSettings.fogDensity = baseFog + cardFog;
            gm.Level.SetSky(Color.Lerp(a.skyColor, b.skyColor, k), Mathf.Lerp(a.sunIntensity, b.sunIntensity, k));
            gm.Level.SetNightLights(Mathf.Lerp(a.torchesLit ? 1 : 0, b.torchesLit ? 1 : 0, k), Mathf.Lerp(a.lanterns ? 1 : 0, b.lanterns ? 1 : 0, k));
        }

        public bool LanternsOn => Current != null && Current.lanterns;
    }
}
