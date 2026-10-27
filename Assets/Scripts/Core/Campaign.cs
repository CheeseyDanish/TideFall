// TideWall 3D — the campaign: the game's parts in order, each with its own scene(s), title, words and rules.
// Lives as Assets/Content/Data/Campaign.asset (edit it in the Inspector); the defaults below create it.
//   Part I  The Wall       defend (the TideWall scene, the endless roguelike)
//   Part II The Undertow   attack (five stages on the drained seabed, one scene each)
// More parts go on the end of `parts`; each stage names the scene that plays it (add it to the build settings).
// The words here are the surface story only. The hidden story lives in symbols: see docs/STORY.md before writing any.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    public enum PartKind { Defense, Assault }

    /// <summary>What a Part II stage asks of the expedition.</summary>
    public enum ObjectiveKind
    {
        Spires,     // topple `count` brood spires
        Beacons,    // light `count` beacons (stand by each until it catches)
        Breach,     // blow open the wreck-wall with fire (gas cans)
        Hold,       // hold the ring for `holdSeconds`
        Heart,      // beat the Tidemother, then choose
    }

    [Serializable]
    public class StageDef
    {
        public string id, title;
        [Tooltip("One line under the title card. Ambiguous, never explanatory (docs/STORY.md)")][TextArea] public string epigraph;
        [Tooltip("The scene that plays this stage (in the build settings)")] public string scene;
        [TextArea] public string objectiveText;
        public ObjectiveKind objective;
        [Tooltip("Spires to topple, beacons to light")] public int count = 3;
        [Tooltip("Hold: seconds to hold out")] public float holdSeconds = 90;
        [Tooltip("The tide clock: seconds before the sea comes back (0 = none)")] public float tideSeconds = 600;
        [Tooltip("Creatures are as tough as on this day of Part I")] public int difficultyDay = 3;
        [Tooltip("Creature ids that come for the expedition here")] public List<string> enemies = new();
        public int maxAlive = 16;
        [Tooltip("Seconds between new creatures (while under maxAlive)")] public float spawnEvery = 5;
        [Tooltip("The stage's light and fog (only the Look fields are used)")] public PhaseDefinition look = new();
    }

    [Serializable]
    public class PartDef
    {
        public string id, numeral, title, subtitle;
        [Tooltip("Shown one line at a time when the part begins")] public List<string> intro = new();
        public PartKind kind;
        [Tooltip("Defense parts: the scene to play")] public string scene;
        [Tooltip("Assault parts: the stages, in order")] public List<StageDef> stages = new();
        [Tooltip("Sand dollars the team starts with")] public int startSandDollars;
        [Tooltip("Players start at this level (with the stat points that come with it)")] public int startLevel = 1;
    }

    [Serializable]
    public class EndingDef
    {
        public string id, title;
        [Tooltip("The choice offered at the Heart")] public string choice;
        public List<string> lines = new();
        [Tooltip("Only offered with all five Spiral Shards")] public bool needsShards;
    }

    [CreateAssetMenu(menuName = "TideWall/Campaign")]
    public class Campaign : ScriptableObject
    {
        public List<PartDef> parts = new();
        public List<EndingDef> endings = new();
        [Tooltip("Spiral Shards there are to find (one per stage of Part II)")] public int shardCount = 5;

        public PartDef Part(string id) => parts.Find(p => p.id == id);
        public EndingDef Ending(string id) => endings.Find(e => e.id == id);

        public static Campaign Load()
        {
            var c = Content.Campaign;
            if (c == null || c.parts.Count == 0) { c = CreateInstance<Campaign>(); c.FillDefaults(); }
            return c;
        }

        /// <summary>The shipped campaign. TideWall → Reset Data To Defaults writes this into the asset.</summary>
        public void FillDefaults()
        {
            // Stage looks: a long, moonlit low tide that gets darker and greener the deeper it goes
            PhaseDefinition Look(Color sky, Color sun, float sunI, float pitch, Color ambient, Color fog, float fogD) => new()
            {
                id = "stage", displayName = "Low Tide", skyColor = sky, sunColor = sun, sunIntensity = sunI, sunPitch = pitch, sunYaw = 160,
                ambientColor = ambient, fogColor = fog, fogDensity = fogD, torchesLit = true, lanterns = true,
            };

            parts = new List<PartDef>
            {
                new()
                {
                    id = "wall", numeral = "I", title = "The Wall", subtitle = "Hold the sea wall. Keep the core alive.",
                    kind = PartKind.Defense, scene = "TideWall",
                    intro =
                    {
                        "Saltmarrow has always had the wall, and the wall has always had the night.",
                        "Every dusk the sea sends its creatures up the sand. Every dawn the town sells what is left of them.",
                        "Keep the Nuclear Sand Powerplant burning. While it burns, Saltmarrow stands.",
                    },
                },
                new()
                {
                    id = "undertow", numeral = "II", title = "The Undertow", subtitle = "Take the war to the sea.",
                    kind = PartKind.Assault, startSandDollars = 400, startLevel = 4,
                    intro =
                    {
                        "The engineers found another setting on the core.",
                        "Run it hot enough and the sea steps back: two walls of standing water, and the seabed open all the way down.",
                        "One night. One tide. March out, find the Heart of the Tide, and end this.",
                    },
                    stages =
                    {
                        new()
                        {
                            id = "ebb", title = "The Ebb", scene = "Undertow 1 - The Ebb", epigraph = "The water stepped aside as if it had been asked.",
                            objective = ObjectiveKind.Spires, count = 4, tideSeconds = 600, difficultyDay = 3, maxAlive = 14, spawnEvery = 5,
                            objectiveText = "Topple the brood spires",
                            enemies = { "crab", "drowner", "hermit", "urchin", "puffer" },
                            look = Look(new Color(.22f, .3f, .48f), new Color(.75f, .82f, 1f), .55f, 28, new Color(.3f, .34f, .45f), new Color(.24f, .3f, .42f), .006f),
                        },
                        new()
                        {
                            id = "kelp", title = "The Kelp Cathedral", scene = "Undertow 2 - The Kelp Cathedral", epigraph = "Someone built these arches. Not us.",
                            objective = ObjectiveKind.Beacons, count = 3, tideSeconds = 600, difficultyDay = 4, maxAlive = 16, spawnEvery = 4.5f,
                            objectiveText = "Light the beacons",
                            enemies = { "drowner", "jelly", "octopus", "crab", "eel" },
                            look = Look(new Color(.16f, .3f, .32f), new Color(.6f, .95f, .85f), .45f, 34, new Color(.22f, .34f, .32f), new Color(.15f, .3f, .28f), .011f),
                        },
                        new()
                        {
                            id = "hulls", title = "The Hull Graveyard", scene = "Undertow 3 - The Hull Graveyard", epigraph = "So many ships. Who sailed them?",
                            objective = ObjectiveKind.Breach, count = 3, tideSeconds = 560, difficultyDay = 5, maxAlive = 16, spawnEvery = 4.5f,
                            objectiveText = "Carry fire to the wreck-wall",
                            enemies = { "croc", "lobster", "hermit", "crab", "turtle" },
                            look = Look(new Color(.2f, .2f, .26f), new Color(.85f, .75f, .65f), .4f, 22, new Color(.27f, .26f, .28f), new Color(.2f, .2f, .24f), .012f),
                        },
                        new()
                        {
                            id = "keep", title = "The Drowned Keep", scene = "Undertow 4 - The Drowned Keep", epigraph = "It has a window that faces the shore.",
                            objective = ObjectiveKind.Hold, holdSeconds = 120, tideSeconds = 520, difficultyDay = 6, maxAlive = 20, spawnEvery = 3,
                            objectiveText = "Hold the keep",
                            enemies = { "drowner", "lobster", "croc", "octopus", "jelly", "eel", "urchin" },
                            look = Look(new Color(.12f, .18f, .2f), new Color(.5f, .9f, .7f), .35f, 40, new Color(.18f, .25f, .22f), new Color(.1f, .18f, .16f), .016f),
                        },
                        new()
                        {
                            id = "heart", title = "The Heart", scene = "Undertow 5 - The Heart", epigraph = "She was waiting.",
                            objective = ObjectiveKind.Heart, tideSeconds = 600, difficultyDay = 6, maxAlive = 10, spawnEvery = 7,
                            objectiveText = "Reach the Heart of the Tide",
                            enemies = { "crab", "drowner", "jelly" },
                            look = Look(new Color(.06f, .1f, .14f), new Color(.45f, 1f, .6f), .3f, 60, new Color(.14f, .22f, .2f), new Color(.05f, .12f, .12f), .02f),
                        },
                    },
                },
            };

            endings = new List<EndingDef>
            {
                new()
                {
                    id = "strike", title = "THE SEA FALLS QUIET", choice = "Strike the Heart",
                    lines =
                    {
                        "The Heart breaks like a wave on stone.",
                        "The water walls come down slowly, almost gently, and the sailors walk home dry.",
                        "Saltmarrow rings every bell it has.",
                        "Someone carves a new mark on the keep wall.",
                        "The next night, the tide comes in.",
                    },
                },
                new()
                {
                    id = "spiral", title = "THE SPIRAL", choice = "Lay down the Crest", needsShards = true,
                    lines =
                    {
                        "The five stones close around the crest.",
                        "The Tidemother lowers her shell.",
                        "Far behind them, on the shore, a green light goes out.",
                        "The tide comes in.",
                        "Nothing comes with it.",
                    },
                },
            };
        }
    }
}
