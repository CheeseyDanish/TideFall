// TideWall 3D — generates the level: ground, sea, the wall (breachable sections), the Nuclear Sand Powerplant, town
// stalls, the keep, lights, clouds and beach decoration. Layout constants are in Arena.cs.
// It runs in the editor (TideWall → Rebuild World In Scene), which saves the result into the TideWall scene; from then
// on the scene is the level and can be edited in Unity like any other.
using UnityEngine;

namespace TideWall
{
    public static class WorldBuilder
    {
        static readonly Color Sand = new(.93f, .82f, .6f), WetSand = new(.7f, .58f, .41f), Seabed = new(.5f, .45f, .35f), Cobble = new(.52f, .5f, .46f), Grass = new(.42f, .58f, .3f);

        public static World Build(Transform parent)
        {
            var root = new GameObject("World");
            if (parent != null) root.transform.SetParent(parent, false);
            var world = root.AddComponent<World>();
            var t = root.transform;
            var db = GameDatabase.Load();

            // --- sun
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(t, false);
            world.sun = sunGo.AddComponent<Light>();
            world.sun.type = LightType.Directional;
            world.sun.shadows = LightShadows.Soft;
            world.sun.shadowStrength = .8f;

            // --- ground: town (cobbles + grass), dry sand, wet sand (glossy), sloping seabed
            Strip(t, "Grass", -140, 140, -70, -24, Grass);
            Strip(t, "Town", -140, 140, -24, -1, Cobble);
            Strip(t, "Beach", -140, 140, -1, Arena.ShoreZ - 6, Sand);
            Strip(t, "WetSand", -140, 140, Arena.ShoreZ - 6, Arena.ShoreZ + .5f, WetSand, smooth: .45f);
            Strip(t, "Seabed", -140, 140, Arena.ShoreZ + .5f, 190, Seabed, 3f);
            Water.Build(t, new Rect(-140, Arena.ShoreZ - 3, 280, 150), 2.5f);

            // --- the wall: sections with their own HP (breachable); sally ports for players
            var wallGo = new GameObject("Wall");
            wallGo.transform.SetParent(t, false);
            world.wall = wallGo.AddComponent<Wall>();
            for (float x = -Arena.WallHalfLength + Arena.SegmentLength / 2; x < Arena.WallHalfLength; x += Arena.SegmentLength)
            {
                if (Arena.IsGap(x))
                {
                    // A gatehouse over the sally port keeps the wall one piece; its pillars are solid, the arch is open
                    var gate = ModelFactory.Spawn("WallGate", wallGo.transform, new Vector3(x, 0, Arena.WallZ));
                    // Banners with the town's Crest, both ways
                    var gb = ModelFactory.Bounds(gate.root);
                    Glyphs.DecalOn(gate.root.transform, "crest", new Vector3(x, gb.max.y - .75f, gb.max.z + .03f), Vector3.forward, .9f, new Color(.8f, .14f, .1f, .95f));
                    Glyphs.DecalOn(gate.root.transform, "crest", new Vector3(x, gb.max.y - .75f, gb.min.z - .03f), Vector3.back, .9f, new Color(.8f, .14f, .1f, .95f));
                    foreach (float px in new[] { -1.71f, 1.71f }) Solid(gate.root, new Vector3(px, 1.45f, 0), new Vector3(.62f, 2.9f, 1.34f));
                    Prop(wallGo.transform, "Crate", new Vector3(x - 2.6f, 0, -1.4f), 20);
                    continue;
                }
                var seg = ModelFactory.Spawn("WallSegment", wallGo.transform, new Vector3(x, 0, Arena.WallZ));
                var col = Solid(seg.root, new Vector3(0, 1.4f, 0), new Vector3(Arena.SegmentLength, 2.8f, 1f));
                world.wall.Add(seg.root.transform, col, db.wallSegmentHp);
            }
            foreach (float sx in new[] { -1f, 1f })
            {
                var tower = ModelFactory.Spawn("Tower", wallGo.transform, new Vector3(sx * (Arena.WallHalfLength + 2), 0, 0));
                Solid(tower.root, new Vector3(0, 2, 0), new Vector3(2.4f, 4, 2.4f));
            }

            // --- the Nuclear Sand Powerplant, centre of town
            world.plant = PowerPlant.Build(t, Arena.PlantPos, db.plantMaxHp);
            // The town's Crest on its landward side. On the side facing the sea, faint in the core's glow: the whole Spiral
            var pb = ModelFactory.Bounds(world.plant.gameObject);
            Glyphs.DecalOn(world.plant.transform, "crest", new Vector3(pb.center.x, pb.min.y + pb.size.y * .45f, pb.min.z - .04f), Vector3.back, 1.8f, new Color(.75f, .12f, .1f, .9f));
            Glyphs.DecalOn(world.plant.transform, "spiral", new Vector3(pb.center.x, pb.min.y + pb.size.y * .55f, pb.max.z + .04f), Vector3.forward, 1.6f, new Color(.45f, 1f, .6f, .45f), glow: true);

            // --- town: stalls face the camera (toward -Z)
            var town = new GameObject("Town").transform;
            town.SetParent(t, false);
            Interactable.Add<ShopKiosk>(Prop(town, "ShopKiosk", new Vector3(-12, 0, -8), 180, solid: new Vector3(2.4f, 2, 1)), "SHOP", 3.2f);
            Interactable.Add<Fishmonger>(Prop(town, "FishmongerCart", new Vector3(-20, 0, -12), 180, solid: new Vector3(2.2f, 2, 1)), "FISHMONGER", 2.8f);
            Interactable.Add<HotdogStand>(Prop(town, "HotdogStand", new Vector3(4, 0, -9), 180, solid: new Vector3(2, 2, 1.2f)), "HOTDOGS", 3.6f);
            // The people behind the stalls (stalls face the camera, toward -Z, so they stand on the far side)
            world.chef = Vendor.Create(town, "VendorChef", new Vector3(4, 0, -10.6f), 180, db.chefLines);
            world.fishmonger = Vendor.Create(town, "VendorFish", new Vector3(-20, 0, -13.6f), 180, db.fishmongerLines);
            Interactable.Add<ReadyBell>(Prop(town, "Bell", new Vector3(0, 0, -4), 0, solid: new Vector3(.3f, 2, .3f)), "READY BELL", 2.8f);

            // --- lights: torches behind the wall and around town, the lighthouse beam
            foreach (var p in new[] { new Vector3(-36, 0, -1.4f), new Vector3(-12, 0, -1.4f), new Vector3(12, 0, -1.4f), new Vector3(36, 0, -1.4f),
                                      new Vector3(-24, 0, -16), new Vector3(30, 0, -9), new Vector3(-8, 0, -21), new Vector3(6, 0, -19) })
            {
                var torch = ModelFactory.Spawn("Torch", town, p);
                var l = new GameObject("TorchLight").AddComponent<Light>();
                l.transform.SetParent(torch.root.transform, false);
                l.transform.localPosition = new Vector3(0, 2.3f, 0);
                l.type = LightType.Point; l.range = 12; l.color = new Color(1, .62f, .3f); l.shadows = LightShadows.None;
                world.torches.Add(l);
            }
            // The keep behind the town: its upper room is the main menu's view out to sea
            world.keep = ModelFactory.Spawn("Keep", t, new Vector3(0, 0, -27.5f));
            var kb = ModelFactory.Bounds(world.keep.root);
            Glyphs.DecalOn(world.keep.root.transform, "crest", new Vector3(kb.center.x - 6, kb.min.y + kb.size.y * .6f, kb.max.z + .04f), Vector3.forward, 2.4f, new Color(.8f, .14f, .1f, .9f));
            Solid(world.keep.root, new Vector3(0, 6.7f, -5f), new Vector3(22, 13.4f, 11.2f));
            foreach (var s in new[] { "TorchL", "TorchR", "Candle" })
                if (world.keep.Sockets.TryGetValue(s, out var sock))
                {
                    var l = new GameObject(s + "Light").AddComponent<Light>();
                    l.transform.SetParent(sock, false);
                    l.type = LightType.Point; l.color = new Color(1, .65f, .35f); l.shadows = LightShadows.None;
                    l.range = s == "Candle" ? 3 : 9; l.intensity = s == "Candle" ? 1.5f : 5;
                }
            var lh = ModelFactory.Spawn("Lighthouse", t, new Vector3(-58, 0, -4));
            world.lighthouseLamp = new GameObject("Beam").transform;
            world.lighthouseLamp.SetParent(lh.root.transform, false);
            world.lighthouseLamp.localPosition = new Vector3(0, 10.2f, 0);
            world.lighthouseBeam = world.lighthouseLamp.gameObject.AddComponent<Light>();
            world.lighthouseBeam.type = LightType.Spot; world.lighthouseBeam.spotAngle = 22; world.lighthouseBeam.range = 120;
            world.lighthouseBeam.color = new Color(1, .95f, .8f); world.lighthouseBeam.shadows = LightShadows.None;

            // --- beach decoration (no colliders except tree trunks, rocks and sandcastles)
            var deco = new GameObject("Beach").transform;
            deco.SetParent(t, false);
            foreach (var (name, pos, rot) in new (string, Vector3, float)[]
            {
                ("PalmTree", new(-42, 0, 14), 0), ("PalmTree", new(38, 0, 10), 140), ("PalmTree", new(-8, 0, 26), 260), ("PalmTree", new(-54, 0, 4), 60),
                ("PalmTree", new(54, 0, -6), 200), ("PalmTree", new(20, 0, 30), 30), ("PalmTree", new(-30, 0, 32), 300),
                ("BeachUmbrella", new(-28, 0, 18), 0), ("Towel", new(-26, 0, 21), 15), ("BeachUmbrella", new(16, 0, 22), 0),
                ("Towel", new(19, 0, 24), -10), ("Towel", new(-2, 0, 12), 80), ("BeachUmbrella", new(34, 0, 28), 0), ("Towel", new(36, 0, 30), 40),
                ("Rock", new(46, 0, 30), 30), ("Rock", new(-48, 0, 32), 110), ("Rock", new(24, 0, 34), 200), ("Rock", new(-14, 0, 36), 70),
                ("Sandcastle", new(-18, 0, 10), 15), ("Sandcastle", new(28, 0, 14), -20), ("Sandcastle", new(6, 0, 33), 40),
                ("Crate", new(-24, 0, -2.2f), 10), ("Crate", new(26, 0, -2), -15),
                ("PalmTree", new(-32, 0, -20), 90), ("PalmTree", new(36, 0, -19), 10), ("PalmTree", new(-60, 0, -14), 45), ("PalmTree", new(60, 0, -12), 160),
            })
            {
                var go = Prop(deco, name, pos, rot);
                if (name == "PalmTree") Solid(go, new Vector3(0, 1.5f, 0), new Vector3(.5f, 3, .5f));
                if (name == "Rock") Solid(go, new Vector3(0, .4f, 0), new Vector3(1.4f, .8f, 1.1f));
                if (name == "Sandcastle") Solid(go, new Vector3(0, .6f, 0), new Vector3(1.6f, 1.2f, 1.6f));
            }
            // Shells, starfish and kelp along the tide line (scenery only)
            var rng = new System.Random(7);
            for (int i = 0; i < 70; i++)
            {
                string m = i % 3 == 0 ? "Starfish" : i % 3 == 1 ? "Shell" : "Kelp";
                float x = (float)(rng.NextDouble() * 96 - 48);
                float z = m == "Kelp" ? Arena.ShoreZ + 14 + (float)rng.NextDouble() * 30 : Arena.ShoreZ - 9 + (float)rng.NextDouble() * 9;
                // Kelp stays under the surface: sized to the depth where it grows
                float size = m == "Kelp" ? Mathf.Clamp(Arena.Depth(new Vector3(x, 0, z)) / 3.6f, .3f, 1.4f) * (.7f + .3f * (float)rng.NextDouble()) : 1.2f;
                var mi = ModelFactory.Spawn(m, deco, new Vector3(x, Arena.GroundY(z), z), size);
                mi.root.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360, 0);
                foreach (var r in mi.Renderers) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            // Clouds drifting high over the sea
            for (int i = 0; i < 14; i++)
            {
                var c = ModelFactory.Spawn("Cloud", t, new Vector3((float)(rng.NextDouble() * 440 - 220), 55 + (float)rng.NextDouble() * 25, 60 + (float)rng.NextDouble() * 150),
                                           1.5f + (float)rng.NextDouble() * 2);
                foreach (var r in c.Renderers) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
                world.clouds.Add(new Level.Cloud { t = c.root.transform, speed = 1.5f + (float)rng.NextDouble() * 2 });
            }

            // --- invisible bounds for players
            var bounds = new GameObject("Bounds").transform;
            bounds.SetParent(t, false);
            Solid(bounds.gameObject, new Vector3(-Arena.BoundX - .5f, 2, 15), new Vector3(1, 4, 80));
            Solid(bounds.gameObject, new Vector3(Arena.BoundX + .5f, 2, 15), new Vector3(1, 4, 80));
            Solid(bounds.gameObject, new Vector3(0, 2, Arena.MinZ - .5f), new Vector3(110, 4, 1));
            Solid(bounds.gameObject, new Vector3(0, 2, Arena.MaxZ + .5f), new Vector3(110, 4, 1));
            return world;
        }

        /// <summary>A flat-shaded ground strip from z0 to z1 that follows Arena.GroundY (sea floor slopes).</summary>
        static void Strip(Transform parent, string name, float x0, float x1, float z0, float z1, Color c, float step = 4f, float smooth = -1)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt((z1 - z0) / step));
            var v = new Vector3[(n + 1) * 2]; var tris = new int[n * 6];
            for (int i = 0; i <= n; i++)
            {
                float z = Mathf.Lerp(z0, z1, i / (float)n), y = Arena.GroundY(z) - .01f;
                v[i * 2] = new Vector3(x0, y, z); v[i * 2 + 1] = new Vector3(x1, y, z);
                if (i < n)
                {
                    int k = i * 6, a = i * 2;
                    tris[k] = a; tris[k + 1] = a + 2; tris[k + 2] = a + 1; tris[k + 3] = a + 1; tris[k + 4] = a + 2; tris[k + 5] = a + 3;
                }
            }
            var mesh = new Mesh { name = name, vertices = v, triangles = tris };
            mesh.RecalculateNormals();
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mat = Mats.Lit(c);
            if (smooth >= 0) { mat = new Material(mat) { name = name }; if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth); }
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        static GameObject Prop(Transform parent, string model, Vector3 pos, float yaw, Vector3? solid = null)
        {
            var mi = ModelFactory.Spawn(model, parent, pos);
            mi.root.transform.rotation = Quaternion.Euler(0, yaw, 0);
            if (solid.HasValue) Solid(mi.root, new Vector3(0, solid.Value.y / 2, 0), solid.Value);
            return mi.root;
        }

        static BoxCollider Solid(GameObject go, Vector3 centre, Vector3 size)
        {
            var bc = go.AddComponent<BoxCollider>();
            bc.center = centre; bc.size = size;
            return bc;
        }
    }
}
