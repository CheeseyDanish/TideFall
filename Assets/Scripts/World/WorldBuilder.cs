// TideWall 3D — builds the level from code: ground, sea, wall, town stalls, lights and decoration.
// Layout constants are in Arena.cs.
using UnityEngine;

namespace TideWall
{
    public static class WorldBuilder
    {
        static readonly Color Sand = new(.93f, .82f, .6f), WetSand = new(.72f, .6f, .42f), Seabed = new(.5f, .45f, .35f), Cobble = new(.52f, .5f, .46f), Grass = new(.42f, .58f, .3f);

        public static World Build(Transform parent)
        {
            var root = new GameObject("World");
            root.transform.SetParent(parent, false);
            var world = root.AddComponent<World>();
            var t = root.transform;

            // --- sun
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(t, false);
            world.sun = sunGo.AddComponent<Light>();
            world.sun.type = LightType.Directional;
            world.sun.shadows = LightShadows.Soft;
            world.sun.shadowStrength = .8f;

            // --- ground: town (cobbles + grass), dry sand, wet sand, sloping seabed
            Strip(t, "Grass", -60, 60, -40, -24, Grass);
            Strip(t, "Town", -60, 60, -24, -1, Cobble);
            Strip(t, "Beach", -60, 60, -1, Arena.ShoreZ - 4, Sand);
            Strip(t, "WetSand", -60, 60, Arena.ShoreZ - 4, Arena.ShoreZ + .5f, WetSand);
            Strip(t, "Seabed", -60, 60, Arena.ShoreZ + .5f, 110, Seabed, 2f);
            Water.Build(t, new Rect(-80, Arena.ShoreZ - 2, 160, 100), 2f);

            // --- the wall
            var wallGo = new GameObject("Wall");
            wallGo.transform.SetParent(t, false);
            world.wall = wallGo.AddComponent<Wall>();
            world.wall.Init(GameManager.I.DB.wallMaxHp);
            for (float x = -Arena.WallHalfLength + Arena.SegmentLength / 2; x < Arena.WallHalfLength; x += Arena.SegmentLength)
            {
                if (Arena.IsGap(x)) { Prop(wallGo.transform, "Crate", new Vector3(x - 1.7f, 0, -1), 20); continue; }
                var seg = ModelFactory.Spawn("WallSegment", wallGo.transform, new Vector3(x, 0, Arena.WallZ));
                Solid(seg.root, new Vector3(0, 1.4f, 0), new Vector3(Arena.SegmentLength, 2.8f, 1f));
                world.wall.AddSegment(seg.root.transform);
            }
            foreach (float sx in new[] { -1f, 1f })
            {
                var tower = ModelFactory.Spawn("Tower", wallGo.transform, new Vector3(sx * (Arena.WallHalfLength + 2), 0, 0));
                Solid(tower.root, new Vector3(0, 2, 0), new Vector3(2.4f, 4, 2.4f));
            }

            // --- town: stalls face the camera (toward -Z)
            var town = new GameObject("Town").transform;
            town.SetParent(t, false);
            Interactable.Add<ShopKiosk>(Prop(town, "ShopKiosk", new Vector3(-9, 0, -10), 180, solid: new Vector3(2.4f, 2, 1)), "SHOP", 3.2f);
            Interactable.Add<Fishmonger>(Prop(town, "FishmongerCart", new Vector3(0, 0, -13), 180, solid: new Vector3(2.2f, 2, 1)), "FISHMONGER", 2.8f);
            Interactable.Add<HotdogStand>(Prop(town, "HotdogStand", new Vector3(9, 0, -10), 180, solid: new Vector3(2, 2, 1.2f)), "HOTDOGS", 3.6f);
            Interactable.Add<ReadyBell>(Prop(town, "Bell", new Vector3(0, 0, -5), 0, solid: new Vector3(.3f, 2, .3f)), "READY BELL", 2.8f);

            // --- lights: torches behind the wall and around town, the lighthouse beam
            foreach (var p in new[] { new Vector3(-18, 0, -1.4f), new Vector3(-6, 0, -1.4f), new Vector3(6, 0, -1.4f), new Vector3(18, 0, -1.4f),
                                      new Vector3(-14, 0, -14), new Vector3(14, 0, -14), new Vector3(-4, 0, -18), new Vector3(4, 0, -18) })
            {
                var torch = ModelFactory.Spawn("Torch", town, p);
                var l = new GameObject("TorchLight").AddComponent<Light>();
                l.transform.SetParent(torch.root.transform, false);
                l.transform.localPosition = new Vector3(0, 2.3f, 0);
                l.type = LightType.Point; l.range = 10; l.color = new Color(1, .62f, .3f); l.shadows = LightShadows.None;
                world.torches.Add(l);
            }
            var lh = ModelFactory.Spawn("Lighthouse", t, new Vector3(-32, 0, -4));
            world.lighthouseLamp = new GameObject("Beam").transform;
            world.lighthouseLamp.SetParent(lh.root.transform, false);
            world.lighthouseLamp.localPosition = new Vector3(0, 10.2f, 0);
            world.lighthouseBeam = world.lighthouseLamp.gameObject.AddComponent<Light>();
            world.lighthouseBeam.type = LightType.Spot; world.lighthouseBeam.spotAngle = 22; world.lighthouseBeam.range = 90;
            world.lighthouseBeam.color = new Color(1, .95f, .8f); world.lighthouseBeam.shadows = LightShadows.None;

            // --- beach decoration (no colliders except tree trunks and rocks)
            var deco = new GameObject("Beach").transform;
            deco.SetParent(t, false);
            foreach (var (name, pos, rot) in new (string, Vector3, float)[]
            {
                ("PalmTree", new(-21, 0, 7), 0), ("PalmTree", new(19, 0, 5), 140), ("PalmTree", new(-4, 0, 13), 260), ("PalmTree", new(-30, 0, 2), 60),
                ("PalmTree", new(30, 0, -6), 200), ("BeachUmbrella", new(-14, 0, 9), 0), ("Towel", new(-13, 0, 10.5f), 15), ("BeachUmbrella", new(8, 0, 11), 0),
                ("Towel", new(9.5f, 0, 12), -10), ("Towel", new(-1, 0, 6), 80), ("Rock", new(24, 0, 15), 30), ("Rock", new(-24, 0, 16), 110),
                ("Rock", new(12, 0, 17), 200), ("Crate", new(-12, 0, -2.2f), 10), ("Crate", new(13, 0, -2), -15), ("PalmTree", new(-16, 0, -20), 90),
                ("PalmTree", new(18, 0, -19), 10),
            })
            {
                var go = Prop(deco, name, pos, rot);
                if (name == "PalmTree") Solid(go, new Vector3(0, 1.5f, 0), new Vector3(.5f, 3, .5f));
                if (name == "Rock") Solid(go, new Vector3(0, .4f, 0), new Vector3(1.4f, .8f, 1.1f));
            }

            // --- invisible bounds for players
            var bounds = new GameObject("Bounds").transform;
            bounds.SetParent(t, false);
            Solid(bounds.gameObject, new Vector3(-Arena.BoundX - .5f, 2, 4), new Vector3(1, 4, 60));
            Solid(bounds.gameObject, new Vector3(Arena.BoundX + .5f, 2, 4), new Vector3(1, 4, 60));
            Solid(bounds.gameObject, new Vector3(0, 2, Arena.MinZ - .5f), new Vector3(60, 4, 1));
            Solid(bounds.gameObject, new Vector3(0, 2, Arena.MaxZ + .5f), new Vector3(60, 4, 1));
            return world;
        }

        /// <summary>A flat-shaded ground strip from z0 to z1 that follows Arena.GroundY (sea floor slopes).</summary>
        static void Strip(Transform parent, string name, float x0, float x1, float z0, float z1, Color c, float step = 4f)
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
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit(c);
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        static GameObject Prop(Transform parent, string model, Vector3 pos, float yaw, Vector3? solid = null)
        {
            var mi = ModelFactory.Spawn(model, parent, pos);
            mi.root.transform.rotation = Quaternion.Euler(0, yaw, 0);
            if (solid.HasValue) Solid(mi.root, new Vector3(0, solid.Value.y / 2, 0), solid.Value);
            return mi.root;
        }

        static void Solid(GameObject go, Vector3 centre, Vector3 size)
        {
            var bc = go.AddComponent<BoxCollider>();
            bc.center = centre; bc.size = size;
        }
    }
}
