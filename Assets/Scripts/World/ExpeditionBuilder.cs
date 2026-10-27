// TideWall 3D — builds a Part II stage (an Expedition) in code: the drained lane of seabed, the two standing walls of
// sea along its sides, the objectives, the cache, the hidden Spiral Shard, the gate on, and each stage's set dressing.
// The set dressing carries the hidden story (docs/STORY.md → "How the symbols tell it, stage by stage"): murals are
// slabs with glyphs cut into them, read left to right, and never explained.
//
// The editor bakes each stage into its own scene (ExpeditionBaker: TideWall → Rebuild Stages); tests build them here.
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    public static class ExpeditionBuilder
    {
        static readonly Color Seabed = new(.55f, .5f, .4f), Silt = new(.42f, .4f, .36f), Sea = new(.12f, .38f, .52f, .62f),
                              Carved = new(.9f, .86f, .74f, .8f), SeaGlow = new(.5f, 1f, .8f, .22f);

        /// <summary>Lengths of the five stages' lanes (metres from the shore end to the far end).</summary>
        static readonly float[] Lengths = { 170, 160, 170, 160, 120 };

        public static Expedition Build(Transform parent, int stage)
        {
            var root = new GameObject($"Expedition {stage + 1}");
            if (parent != null) root.transform.SetParent(parent, false);
            var ex = root.AddComponent<Expedition>();
            var t = root.transform;
            ex.stageIndex = stage;
            ex.halfWidth = 24;
            ex.length = Lengths[Mathf.Clamp(stage, 0, Lengths.Length - 1)];
            float hw = ex.halfWidth, len = ex.length;
            var rng = new System.Random(4100 + stage);

            // --- the moon (the stages are one night)
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(t, false);
            ex.sun = sunGo.AddComponent<Light>();
            ex.sun.type = LightType.Directional;
            ex.sun.shadows = LightShadows.Soft;
            ex.sun.shadowStrength = .7f;

            // --- the floor: wet sand down the middle, darker silt toward the water
            Ground(t, "Seabed", -hw + 6, hw - 6, -12, len + 12, Seabed, .35f);
            Ground(t, "SiltL", -hw - 4, -hw + 6, -12, len + 12, Silt, .55f);
            Ground(t, "SiltR", hw - 6, hw + 4, -12, len + 12, Silt, .55f);

            // --- the sea, standing on both sides as a wall (and behind the far end), with the Spiral in it
            var sea = new GameObject("Standing sea").transform;
            sea.SetParent(t, false);
            foreach (float side in new[] { -1f, 1f })
            {
                Box(sea, $"Sea wall {(side < 0 ? "L" : "R")}", new Vector3(side * (hw + 1.5f), 7, len / 2), new Vector3(3, 14, len + 30), Mats.Transparent(Sea), solid: true);
                Box(sea, "Foam", new Vector3(side * (hw - .1f), .08f, len / 2), new Vector3(.6f, .16f, len + 30), Mats.Lit(new Color(.88f, .94f, .96f)), solid: false);
                for (float z = 18; z < len; z += 34)
                    Glyphs.Decal(sea, "spiral", new Vector3(side * (hw - .05f), 6.5f + (float)rng.NextDouble() * 3, z), Quaternion.Euler(0, side * 90, 0), 5, SeaGlow, glow: true);
            }
            Box(sea, "Sea wall far", new Vector3(0, 7, len + 14), new Vector3(hw * 2 + 6, 14, 3), Mats.Transparent(Sea), solid: true);
            Box(sea, "Behind", new Vector3(0, 2, -11), new Vector3(hw * 2, 4, 1), null, solid: true);   // the way home is closed for tonight

            // --- where creatures step out of the walls: every 12 m down both sides
            var spawns = new GameObject("Creature spawns").transform;
            spawns.SetParent(t, false);
            for (float z = 14; z < len - 4; z += 12)
                foreach (float side in new[] { -1f, 1f })
                {
                    var s = new GameObject($"Spawn {(side < 0 ? "L" : "R")} {z}").transform;
                    s.SetParent(spawns, false);
                    s.localPosition = new Vector3(side * (hw - 1.5f), 0, z);
                    ex.creatureSpawns.Add(s);
                }

            // --- the start: the expedition walks in from the shore end; the cache it sent ahead
            var start = new GameObject("Player start").transform;
            start.SetParent(t, false);
            start.localPosition = new Vector3(0, 0, 4);
            ex.playerStart = start;
            SupplyCache.Create(t, new Vector3(-7, 0, 9), 160);

            // --- scattered kelp and rock
            var dress = new GameObject("Seabed dressing").transform;
            dress.SetParent(t, false);
            for (int i = 0; i < 46; i++)
            {
                float side = rng.NextDouble() < .5 ? -1 : 1;
                var at = new Vector3(side * (hw - 2 - (float)rng.NextDouble() * 8), 0, 6 + (float)rng.NextDouble() * (len - 10));
                bool rock = i % 3 == 0;
                var m = ModelFactory.Spawn(rock ? "Rock" : "Kelp", dress, at, rock ? 1 + (float)rng.NextDouble() * 1.4f : 1.6f + (float)rng.NextDouble() * 2.2f, colliders: rock, triggers: false);
                m.root.transform.localRotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360, 0);
            }

            // --- the stage itself
            switch (stage)
            {
                case 0: Ebb(ex, t, rng); break;
                case 1: KelpCathedral(ex, t, rng); break;
                case 2: HullGraveyard(ex, t, rng); break;
                case 3: DrownedKeep(ex, t, rng); break;
                default: Heart(ex, t); break;
            }

            if (stage < 4) ex.exit = ExitGate.Create(t, new Vector3(0, 0, len - 5));
            return ex;
        }

        // ------------------------------------------------------------------ the five stages
        /// <summary>The Ebb: brood spires down the lane; Saltmarrow's own boats beached on the way, crest on every one.</summary>
        static void Ebb(Expedition ex, Transform t, System.Random rng)
        {
            float[] z = { 38, 72, 104, 138 };
            for (int i = 0; i < z.Length; i++)
                ex.objectives.Add(BroodSpire.Create(t, new Vector3(i % 2 == 0 ? -8 : 9, 0, z[i]), i % 2 == 0 ? 20 : -25));
            // Beached boats: the town's, left by the last tide that went out this far
            foreach (var (pos, yaw) in new[] { (new Vector3(14, 0, 22), 70f), (new Vector3(-17, 0, 55), -40f), (new Vector3(16, 0, 90), 120f), (new Vector3(-15, 0, 120), 10f) })
                Boat(t, pos, yaw);
            ShardStone.Create(t, new Vector3(-20.5f, 0, 58), 0);   // tucked behind the second boat, against the water
        }

        /// <summary>The Kelp Cathedral: arches older than the town, murals of the pact, beacons that are the sea's own lamps.</summary>
        static void KelpCathedral(Expedition ex, Transform t, System.Random rng)
        {
            for (float z = 20; z < ex.length - 14; z += 16)
            {
                var arch = ModelFactory.Spawn("Arch", t, new Vector3(0, 0, z), 2.6f, colliders: true, triggers: false);
                arch.root.name = "Arch";
            }
            float[] bz = { 42, 88, 128 };
            for (int i = 0; i < bz.Length; i++)
                ex.objectives.Add(Beacon.Create(t, new Vector3(i == 1 ? 10 : -10, 0, bz[i]), 0));
            // The pact, in four panels: the sea and the five; two hands; the heart passing from the sea to them; a calm wave
            Mural(t, new Vector3(-17, 0, 30), Vector3.right, "spiral", "figure", "petals");
            Mural(t, new Vector3(17, 0, 60), Vector3.left, "hand", "hand");
            Mural(t, new Vector3(-17, 0, 100), Vector3.right, "wave", "heart", "figure");
            Mural(t, new Vector3(17, 0, 140), Vector3.left, "wave", "spiral");
            ShardStone.Create(t, new Vector3(20.5f, 0, 112), 1);   // beside the third arch's far pillar, in the kelp
        }

        /// <summary>The Hull Graveyard: a wall of Saltmarrow wrecks across the lane, and one wreck for every run you've played.</summary>
        static void HullGraveyard(Expedition ex, Transform t, System.Random rng)
        {
            ex.objectives.Add(WreckBarrier.Create(t, new Vector3(0, 0, 128), ex.halfWidth, new Vector3(7, 0, 112)));
            ex.memorialField = new Bounds(new Vector3(0, 0, 70), new Vector3(36, 1, 80));
            // The old wrecks, always here (the memorials of your own runs are laid among them when the stage starts)
            for (int i = 0; i < 6; i++)
                Boat(t, new Vector3((i % 2 == 0 ? -1 : 1) * (12 + (float)rng.NextDouble() * 6), 0, 24 + i * 16), (float)rng.NextDouble() * 360, wrecked: true);
            ShardStone.Create(t, new Vector3(-21, 0, 116), 2);   // in the corner before the wreck-wall
        }

        /// <summary>The Drowned Keep: the town's keep, stone for stone, on the sea floor. The Eye over its door; the
        /// theft on the slabs before it. Hold the ring before its window.</summary>
        static void DrownedKeep(Expedition ex, Transform t, System.Random rng)
        {
            float kz = 118;
            var keep = ModelFactory.Spawn("Keep", t, new Vector3(0, 0, kz), 1, colliders: true, triggers: false);
            keep.root.transform.localRotation = Quaternion.Euler(0, 180, 0);   // its window faces the shore
            var b = ModelFactory.Bounds(keep.root);
            Glyphs.DecalOn(t, "eye", new Vector3(0, b.min.y + b.size.y * .62f, b.min.z - .06f), Vector3.back, 2.2f, new Color(.6f, 1f, .85f, .85f), glow: true);
            Glyphs.DecalOn(t, "crest", new Vector3(0, b.min.y + b.size.y * .82f, b.min.z - .06f), Vector3.back, 1.6f, Carved);
            // The first wall: broken sections in front of it
            for (int i = 0; i < 7; i++)
            {
                float x = -18 + i * 6;
                if (Mathf.Abs(x) < 5) continue;
                string m = i % 2 == 0 ? "WallSegment" : "WallRubble";
                var w = ModelFactory.Spawn(m, t, new Vector3(x, 0, kz - 34), 1, colliders: true, triggers: false);
                w.root.transform.localRotation = Quaternion.Euler(0, (float)rng.NextDouble() * 16 - 8, (i % 2 == 0 ? 1 : 0) * ((float)rng.NextDouble() * 10 - 5));
            }
            ex.objectives.Add(HoldRing.Create(t, new Vector3(0, 0, kz - 20), 11, 120));   // (the stage's holdSeconds when it starts)
            // The theft: the five carry the heart away; a keep; the wave rises; a wall
            Mural(t, new Vector3(-17, 0, 46), Vector3.right, "figure", "heart", "hand");
            Mural(t, new Vector3(17, 0, 64), Vector3.left, "keep", "heart");
            Mural(t, new Vector3(-17, 0, 82), Vector3.right, "wave", "keep");
            Mural(t, new Vector3(17, 0, 100), Vector3.left, "crest", "tally");
            ShardStone.Create(t, new Vector3(0, 0, kz + 14), 3);   // round the back of the keep
        }

        /// <summary>The Heart: the trench's end. The Tidemother, the Heart she grew, and the hollow beside it.</summary>
        static void Heart(Expedition ex, Transform t)
        {
            float az = ex.length - 30;
            ex.objectives.Add(HeartAltar.Create(t, new Vector3(0, 0, az), new Vector3(0, 0, az - 12)));
            Mural(t, new Vector3(-17, 0, 30), Vector3.right, "mark", "crest", "spiral");
            ShardStone.Create(t, new Vector3(19, 0, az + 6), 4);   // behind the hollow's side
        }

        // ------------------------------------------------------------------ pieces
        /// <summary>A Saltmarrow boat: a hull with a mast and the crest on its sail (or a wreck: on its side, crest and tally).</summary>
        static void Boat(Transform t, Vector3 pos, float yaw, bool wrecked = false)
        {
            var hull = ModelFactory.Spawn("WreckHull", t, pos, 1.1f, colliders: true, triggers: false);
            hull.root.transform.localRotation = Quaternion.Euler(0, yaw, wrecked ? 55 : 14);
            Glyphs.Decal(hull.root.transform, "crest", new Vector3(.62f, 1.1f, 0), Quaternion.Euler(0, -90, 0), 1.2f, Carved);
            if (wrecked) { Glyphs.Decal(hull.root.transform, "tally", new Vector3(.62f, .5f, 1.2f), Quaternion.Euler(0, -90, 0), .7f, Carved); return; }
            // The sail: a pale cloth on a mast, the crest on it
            var sail = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sail.name = "Sail";
            Object.DestroyImmediate(sail.GetComponent<Collider>());
            sail.transform.SetParent(hull.root.transform, false);
            sail.transform.localPosition = new Vector3(0, 3.2f, .3f);
            sail.transform.localRotation = Quaternion.Euler(0, 90, 0);
            sail.transform.localScale = new Vector3(2.2f, 2.4f, 1);
            sail.GetComponent<Renderer>().sharedMaterial = Mats.Lit(new Color(.86f, .8f, .68f));
            Glyphs.Decal(sail.transform, "crest", new Vector3(0, 0, -.01f), Quaternion.identity, .7f, new Color(.55f, .12f, .1f, .9f));
            Glyphs.Decal(sail.transform, "crest", new Vector3(0, 0, .01f), Quaternion.Euler(0, 180, 0), .7f, new Color(.55f, .12f, .1f, .9f));
        }

        /// <summary>A carved slab beside the lane, its face toward `facing`, with glyphs cut into it left to right.</summary>
        public static GameObject Mural(Transform t, Vector3 pos, Vector3 facing, params string[] glyphs)
        {
            var slab = ModelFactory.Spawn("MuralSlab", t, pos, 1, colliders: true, triggers: false).root;
            slab.name = "Mural " + string.Join(" ", glyphs);
            slab.transform.localRotation = Quaternion.LookRotation(-facing);   // the slab's -Z face looks along `facing`
            float step = 2.6f / glyphs.Length;
            for (int i = 0; i < glyphs.Length; i++)
                Glyphs.Decal(slab.transform, glyphs[i], new Vector3(-1.3f + step * (i + .5f), 1.6f, -.27f), Quaternion.identity, Mathf.Min(1.4f, step * .9f), Carved);
            return slab;
        }

        static void Ground(Transform parent, string name, float x0, float x1, float z0, float z1, Color c, float smooth)
        {
            var mesh = new Mesh
            {
                name = name,
                vertices = new[] { new Vector3(x0, -.01f, z0), new Vector3(x1, -.01f, z0), new Vector3(x0, -.01f, z1), new Vector3(x1, -.01f, z1) },
                triangles = new[] { 0, 2, 1, 1, 2, 3 },
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mat = new Material(Mats.Lit(c)) { name = name };
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        static GameObject Box(Transform parent, string name, Vector3 centre, Vector3 size, Material mat, bool solid)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localScale = size;
            if (mat != null)
            {
                var r = go.GetComponent<Renderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            else Object.DestroyImmediate(go.GetComponent<Renderer>());
            if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
    }
}
