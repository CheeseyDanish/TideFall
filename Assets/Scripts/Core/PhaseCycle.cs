// TideWall 3D — the day is a loop of phases (Morning → Evening → Night → Morning…).
// Phases are pure data: add, remove or reorder them in Assets/Resources/Data/PhaseCycle.asset and the
// PhaseManager runs whatever is listed. Nothing in code refers to a phase by name.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    [Serializable]
    public class PhaseDefinition
    {
        public string id = "phase";
        public string displayName = "Phase";
        [TextArea] public string subtitle;

        [Header("How the phase ends (any ticked condition ends it)")]
        [Tooltip("0 = no timer")] public float durationSeconds;
        [Tooltip("Ends when every player has rung the bell")] public bool endWhenAllReady;
        [Tooltip("Ends when all of this phase's waves are spawned and dead")] public bool endWhenWavesCleared;
        [Tooltip("The day counter goes up when this phase ends")] public bool advancesDay;
        [Tooltip("Autosave when this phase starts; players can also save from the pause menu during it")] public bool saveOnStart;

        [Header("What happens")]
        public bool shopOpen;
        [Tooltip("Number of enemy waves (0 = peaceful)")] public int waves;
        [Tooltip("Multiplies the day's spawn budget")] public float budgetMultiplier = 1;
        [Tooltip("Enemy ids allowed in this phase (empty = all)")] public List<string> enemyPool = new();
        [Tooltip("Every Nth day, this phase also brings the boss (0 = never)")] public int bossEveryDays;
        public string bossId = "glizzy";
        [Tooltip("Picnic leftovers appear on the beach when the phase starts")] public bool spawnFood;
        [Tooltip("Gas cans dropped on the beach when the phase starts (plus upgrades)")] public bool spawnGasCans;

        [Header("Look")]
        public Color skyColor = new(.55f, .75f, .95f);
        public Color sunColor = Color.white;
        [Range(0, 3)] public float sunIntensity = 1.2f;
        [Tooltip("Sun elevation in degrees")] public float sunPitch = 50;
        public float sunYaw = -30;
        public Color ambientColor = new(.5f, .55f, .6f);
        public Color fogColor = new(.7f, .8f, .9f);
        [Range(0, .05f)] public float fogDensity = .004f;
        public bool torchesLit;
        [Tooltip("Players carry a lantern")] public bool lanterns;
    }

    [CreateAssetMenu(menuName = "TideWall/Phase Cycle")]
    public class PhaseCycle : ScriptableObject
    {
        public List<PhaseDefinition> phases = new();

        public static PhaseCycle Load()
        {
            var c = Resources.Load<PhaseCycle>("Data/PhaseCycle");
            if (c == null || c.phases.Count == 0) { c = CreateInstance<PhaseCycle>(); c.FillDefaults(); }
            return c;
        }

        public void FillDefaults()
        {
            phases = new List<PhaseDefinition>
            {
                new()
                {
                    id = "morning", displayName = "Morning", subtitle = "Sell the night's catch, eat, upgrade. Ring the bell when you're ready.",
                    durationSeconds = 150, endWhenAllReady = true, shopOpen = true, spawnFood = true, spawnGasCans = true, saveOnStart = true,
                    skyColor = new Color(.62f, .8f, .96f), sunColor = new Color(1f, .93f, .8f), sunIntensity = 1.3f, sunPitch = 30, sunYaw = -70,
                    ambientColor = new Color(.62f, .62f, .6f), fogColor = new Color(.78f, .86f, .93f), fogDensity = .003f,
                },
                new()
                {
                    id = "evening", displayName = "Evening", subtitle = "The tide turns. Something is coming ashore.",
                    endWhenWavesCleared = true, waves = 2, budgetMultiplier = .55f,
                    enemyPool = new List<string> { "crab", "drowner", "gull", "hermit", "puffer", "jelly", "urchin", "eel", "shark" },
                    skyColor = new Color(.95f, .55f, .35f), sunColor = new Color(1f, .6f, .35f), sunIntensity = 1.0f, sunPitch = 10, sunYaw = 110,
                    ambientColor = new Color(.55f, .4f, .38f), fogColor = new Color(.9f, .55f, .4f), fogDensity = .006f, torchesLit = true,
                },
                new()
                {
                    id = "night", displayName = "Night", subtitle = "Hold the wall until dawn.",
                    endWhenWavesCleared = true, advancesDay = true, waves = 3, budgetMultiplier = 1f, bossEveryDays = 5,
                    skyColor = new Color(.04f, .06f, .14f), sunColor = new Color(.55f, .65f, 1f), sunIntensity = .35f, sunPitch = 45, sunYaw = 160,
                    ambientColor = new Color(.13f, .15f, .25f), fogColor = new Color(.05f, .07f, .15f), fogDensity = .012f, torchesLit = true, lanterns = true,
                },
            };
        }
    }
}
