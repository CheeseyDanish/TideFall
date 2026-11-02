// TideWall 3D — builds The Last Light's level in code: a hill of sand in the open sea (its height is Arena.IslandY, so what
// you see is what the rules use), the lighthouse on top with its lamp, the fishmonger, supplies and a hotdog stand, some
// palms and rocks. The editor bakes it into its own scene (LighthouseBaker: TideWall → Rebuild The Last Light).
using UnityEngine;

namespace TideWall
{
    public static class LighthouseBuilder
    {
        public static Lighthouse Build(Transform parent)
        {
            var root = new GameObject("The Last Light");
            if (parent != null) root.transform.SetParent(parent, false);
            var level = root.AddComponent<Lighthouse>();
            var t = root.transform;

            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(t, false);
            level.sun = sunGo.AddComponent<Light>();
            level.sun.type = LightType.Directional;
            level.sun.shadows = LightShadows.Soft;
            level.sun.shadowStrength = .75f;

            Ground(t);
            var sea = Water.Build(t, new Rect(-130, -130, 260, 260), 3.5f);
            sea.DropFoam();
            // Past the waves, flat water out to the horizon (it moves with the sea as the tide does)
            var far = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Util.Destroy(far.GetComponent<Collider>());
            far.name = "Far Sea";
            far.transform.SetParent(sea.transform, false);
            far.transform.localPosition = Vector3.down * .3f;
            far.transform.localRotation = Quaternion.Euler(90, 0, 0);
            far.transform.localScale = Vector3.one * 1800;
            far.GetComponent<MeshRenderer>().sharedMaterial = Mats.Lit(new Color(.13f, .42f, .62f));
            far.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            float top = Arena.IslandTop;
            var tower = ModelFactory.Spawn("Lighthouse", t, new Vector3(0, top, 0), 1.25f);
            var col = tower.root.AddComponent<CapsuleCollider>();
            col.radius = 2.4f; col.height = 12; col.center = new Vector3(0, 6, 0);
            var b = ModelFactory.Bounds(tower.root);
            float roomY = b.max.y - 1.6f;
            level.lampHeight = roomY - top - 1;

            var lampGo = new GameObject("Lamp");
            lampGo.transform.SetParent(t, false);
            lampGo.transform.position = new Vector3(0, roomY, 0);
            level.lamp = lampGo.transform;
            var beam = ModelFactory.Spawn("LampBeam", level.lamp, Vector3.zero);
            foreach (var r in beam.Renderers) { r.sharedMaterial = Mats.ParticleAdditive; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
            level.beam = beam.root.transform;
            var lightGo = new GameObject("Beam Light");
            lightGo.transform.SetParent(level.lamp, false);
            level.beamLight = lightGo.AddComponent<Light>();
            level.beamLight.type = LightType.Spot;
            level.beamLight.range = 70; level.beamLight.intensity = 60; level.beamLight.color = new Color(1, .9f, .65f);
            level.beamLight.shadows = LightShadows.None;

            // The way up: the door at the foot of the tower
            var door = new GameObject("Lamp Room Door");
            door.transform.SetParent(t, false);
            door.transform.position = new Vector3(0, top, -3.1f);
            Interactable.Add<LampDoor>(door, "LAMP ROOM", 3f);

            var camp = new GameObject("Camp").transform;
            camp.SetParent(t, false);
            camp.position = new Vector3(0, top, 0);
            SupplyCache.Create(camp, new Vector3(6.5f, 0, 3), 250);
            Interactable.Add<Fishmonger>(WorldBuilder.Prop(camp, "FishmongerCart", new Vector3(-6.5f, 0, 3), 110, solid: new Vector3(2.2f, 2, 1)), "TRY-POT", 2.8f);
            Interactable.Add<HotdogStand>(WorldBuilder.Prop(camp, "HotdogStand", new Vector3(1, 0, 7.5f), 180, solid: new Vector3(2, 2, 1.2f)), "HOTDOGS", 3.6f);

            // Palms, rocks and torches down the hill, so you can see how far the sea has come
            var rng = new System.Random(5150);
            var dress = new GameObject("Hillside").transform;
            dress.SetParent(t, false);
            for (int i = 0; i < 26; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2, d = 11 + (float)rng.NextDouble() * 34;
                var at = new Vector3(Mathf.Cos(a) * d, 0, Mathf.Sin(a) * d);
                at.y = Arena.IslandY(at.x, at.z);
                string model = i % 3 == 0 ? "PalmTree" : i % 3 == 1 ? "Rock" : "Torch";
                var m = ModelFactory.Spawn(model, dress, at, model == "Torch" ? 1 : .7f + (float)rng.NextDouble() * .6f);
                m.root.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360, 0);
                if (model == "Torch" && m.Sockets.TryGetValue("Flame", out var flame))
                {
                    var l = new GameObject("Torch Light").AddComponent<Light>();
                    l.transform.SetParent(flame, false);
                    l.type = LightType.Point; l.range = 8; l.color = new Color(1, .65f, .3f); l.shadows = LightShadows.None;
                    level.torches.Add(l);
                }
            }
            return level;
        }

        /// <summary>The hill as a mesh of rings: pale dry sand on top, darkening toward the seabed.</summary>
        static void Ground(Transform t)
        {
            const int Rings = 70, Sides = 160;
            const float MaxR = 130;
            var v = new Vector3[Rings * Sides + 1];
            var c = new Color[v.Length];
            var tris = new int[(Rings - 1) * Sides * 6 + Sides * 3];
            v[0] = new Vector3(0, Arena.IslandY(0, 0), 0);
            c[0] = Color.white;
            int ti = 0;
            for (int r = 0; r < Rings; r++)
            {
                float rr = Mathf.Pow((r + 1) / (float)Rings, 1.5f) * MaxR;
                for (int s = 0; s < Sides; s++)
                {
                    float a = s / (float)Sides * Mathf.PI * 2;
                    float x = Mathf.Cos(a) * rr, z = Mathf.Sin(a) * rr;
                    float y = Arena.IslandY(x, z);
                    v[1 + r * Sides + s] = new Vector3(x, y, z);
                    float wet = Mathf.InverseLerp(Arena.IslandTop * .6f, -1.5f, y);
                    c[1 + r * Sides + s] = Color.Lerp(Color.white, new Color(.62f, .55f, .46f), wet);
                }
            }
            for (int s = 0; s < Sides; s++)
            {
                tris[ti++] = 0; tris[ti++] = 1 + (s + 1) % Sides; tris[ti++] = 1 + s;
            }
            for (int r = 0; r < Rings - 1; r++)
                for (int s = 0; s < Sides; s++)
                {
                    int a = 1 + r * Sides + s, b = 1 + r * Sides + (s + 1) % Sides, d = 1 + (r + 1) * Sides + s, e = 1 + (r + 1) * Sides + (s + 1) % Sides;
                    tris[ti++] = a; tris[ti++] = b; tris[ti++] = d;
                    tris[ti++] = b; tris[ti++] = e; tris[ti++] = d;
                }
            var mesh = new Mesh { name = "Island", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, vertices = v, colors = c, triangles = tris };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go = new GameObject("Island");
            go.transform.SetParent(t, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit(new Color(1f, .9f, .66f));
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }
    }
}
