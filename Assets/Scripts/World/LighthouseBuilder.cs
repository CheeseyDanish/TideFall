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
            far.GetComponent<MeshRenderer>().sharedMaterial = Mats.Lit(new Color(.25f, .57f, .79f));
            far.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            float top = Arena.IslandTop;
            var towerAt = new GameObject("Lighthouse").transform;
            towerAt.SetParent(t, false);
            towerAt.position = new Vector3(0, top, 0);
            LighthouseTower.Build(towerAt);
            float roomY = top + LighthouseTower.LampHeight;
            level.lampHeight = LighthouseTower.LampHeight;

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

            // The lamp itself glows, and beside it you can take it over or set off the flare
            var glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Util.Destroy(glow.GetComponent<Collider>());
            glow.name = "Lamp Glow";
            glow.transform.SetParent(level.lamp, false);
            glow.transform.localScale = Vector3.one * 2.6f;
            glow.GetComponent<MeshRenderer>().sharedMaterial = Mats.ParticleAdditive;
            glow.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            level.lampGlow = glow.transform;
            var flareGlow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Util.Destroy(flareGlow.GetComponent<Collider>());
            flareGlow.name = "Flare Glow";
            flareGlow.transform.SetParent(level.lamp, false);
            flareGlow.GetComponent<MeshRenderer>().sharedMaterial = Mats.ParticleAdditive;
            flareGlow.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            flareGlow.SetActive(false);
            level.flareGlow = flareGlow.transform;
            var flareSun = new GameObject("Flare Light");
            flareSun.transform.SetParent(t, false);
            flareSun.transform.rotation = Quaternion.Euler(80, 0, 0);
            level.flareLight = flareSun.AddComponent<Light>();
            level.flareLight.type = LightType.Directional; level.flareLight.color = new Color(1, .96f, .86f); level.flareLight.shadows = LightShadows.None;
            level.flareLight.enabled = false;
            float deck = top + LighthouseTower.DeckHeight;
            Interactable.Add<LampDoor>(Marker(t, "Lamp Handle", new Vector3(0, deck, -1.6f)), "LAMP", 2.2f);
            var lever = Marker(t, "Flare Lever", new Vector3(2.4f, deck, 0));
            LeverPart(lever.transform, new Vector3(0, .5f, 0), new Vector3(.5f, 1, .3f), Vector3.zero, new Color(.3f, .3f, .33f));
            LeverPart(lever.transform, new Vector3(0, 1.3f, 0), new Vector3(.09f, .9f, .09f), new Vector3(0, 0, -20), new Color(.72f, .56f, .24f));
            LeverPart(lever.transform, new Vector3(.16f, 1.72f, 0), new Vector3(.2f, .2f, .2f), Vector3.zero, new Color(.85f, .15f, .12f));
            Interactable.Add<LampFlare>(lever, "FLARE", 2.2f);

            // The ground floor: the shop, and the calendar on the wall
            Interactable.Add<ShopKiosk>(WorldBuilder.Prop(t, "ShopKiosk", new Vector3(0, top + .05f, 2.8f), 180, solid: new Vector3(2.4f, 2, 1)), "SHOP", 3.2f);
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Util.Destroy(board.GetComponent<Collider>());
            board.name = "Calendar";
            board.transform.SetParent(t, false);
            board.transform.position = new Vector3(5.45f, top + 1.9f, 0);
            board.transform.rotation = Quaternion.LookRotation(Vector3.right);
            board.transform.localScale = new Vector3(2.5f, 2.3f, .06f);
            board.GetComponent<MeshRenderer>().sharedMaterial = Mats.Lit(new Color(.92f, .87f, .72f));
            var page = new GameObject("Page").transform;
            page.SetParent(t, false);
            page.position = board.transform.position + Vector3.left * .05f;
            page.rotation = board.transform.rotation;
            level.calendar = page;
            Reef(t, level, sea.transform);

            var camp = new GameObject("Camp").transform;
            camp.SetParent(t, false);
            camp.position = new Vector3(0, top, 0);
            SupplyCache.Create(camp, new Vector3(8.8f, 0, 3), 250);
            Interactable.Add<Fishmonger>(WorldBuilder.Prop(camp, "FishmongerCart", new Vector3(-8.8f, 0, 3), 110, solid: new Vector3(2.2f, 2, 1)), "TRY-POT", 2.8f);
            Interactable.Add<HotdogStand>(WorldBuilder.Prop(camp, "HotdogStand", new Vector3(1, 0, 9.6f), 180, solid: new Vector3(2, 2, 1.2f)), "HOTDOGS", 3.6f);

            // Palms, rocks and torches down the hill, so you can see how far the sea has come
            var rng = new System.Random(5150);
            var dress = new GameObject("Hillside").transform;
            dress.SetParent(t, false);
            for (int i = 0; i < 26; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2, d = 12 + (float)rng.NextDouble() * 33;
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

        static void LeverPart(Transform t, Vector3 at, Vector3 size, Vector3 turn, Color c)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Util.Destroy(part.GetComponent<Collider>());
            part.name = "Lever Part";
            part.transform.SetParent(t, false);
            part.transform.localPosition = at;
            part.transform.localRotation = Quaternion.Euler(turn);
            part.transform.localScale = size;
            part.GetComponent<MeshRenderer>().sharedMaterial = Mats.Lit(c);
        }

        static GameObject Marker(Transform t, string name, Vector3 at)
        {
            var go = new GameObject(name);
            go.transform.SetParent(t, false);
            go.transform.position = at;
            return go;
        }

        /// <summary>What the light is for: rocks and the ships they've already had, out past where you can wade. And, in the
        /// dark, something looking back from the water.</summary>
        static void Reef(Transform t, Lighthouse level, Transform sea)
        {
            var rng = new System.Random(8128);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var reef = new GameObject("Reef").transform;
            reef.SetParent(t, false);
            void Place(string model, float rFrom, float rTo, float size, float sink, float tilt)
            {
                float a = R(0, Mathf.PI * 2), d = R(rFrom, rTo);
                var at = new Vector3(Mathf.Cos(a) * d, 0, Mathf.Sin(a) * d);
                at.y = Arena.IslandY(at.x, at.z) - sink;
                var m = ModelFactory.Spawn(model, reef, at, size);
                m.root.transform.rotation = Quaternion.Euler(R(-tilt, tilt), R(0, 360), R(-tilt, tilt));
            }
            for (int i = 0; i < 60; i++) Place("Rock", 68, 128, R(5, 16), R(0, 1), 18);
            for (int i = 0; i < 12; i++) Place("WreckHull", 72, 125, R(4.5f, 8), 0, 28);
            for (int i = 0; i < 5; i++) Place("Anchor", 70, 110, R(3, 5), 1, 35);

            var eyes = new GameObject("Eyes");
            eyes.transform.SetParent(sea, false);
            for (int i = 0; i < 26; i++)
            {
                float a = R(0, 360), d = R(48, 64), gap = R(.5f, .9f);
                var at = Quaternion.Euler(0, a, 0) * Vector3.forward * d + Vector3.up * R(.4f, .9f);
                var side = Quaternion.Euler(0, a, 0) * Vector3.right * gap;
                foreach (var o in new[] { at - side, at + side })
                {
                    var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Util.Destroy(eye.GetComponent<Collider>());
                    eye.name = "Eye";
                    eye.transform.SetParent(eyes.transform, false);
                    eye.transform.localPosition = o;
                    eye.transform.localScale = Vector3.one * .28f;
                    eye.GetComponent<MeshRenderer>().sharedMaterial = Mats.ParticleAdditive;
                    eye.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
            eyes.SetActive(false);
            level.eyes = eyes;
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
