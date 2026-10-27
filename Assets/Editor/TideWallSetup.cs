// TideWall 3D — one-click project setup (menu: TideWall → Setup Project, or headless:
//   Unity -batchmode -projectPath . -executeMethod TideWall.EditorTools.TideWallSetup.Run -quit).
// Idempotent: creates whatever is missing — base materials, data assets, the sound files, the content library, the
// game scene (post-processing, the GameManager, and the level the first time), and build/player settings.
// It never overwrites edits: "Reset Data" rewrites the data from the code defaults, "Rebuild World In Scene"
// regenerates the level, and "Bake Sounds" rewrites the WAVs, each only when asked.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TideWall.EditorTools
{
    public static class TideWallSetup
    {
        public const string ScenePath = "Assets/Scenes/TideWall.unity";
        public const string LibraryPath = "Assets/Content/ContentLibrary.asset";
        const string SoundsPath = "Assets/Content/Sounds";
        const string GlyphsPath = "Assets/Content/Glyphs";

        [UnityEditor.MenuItem("TideWall/Setup Project")]
        public static void Run()
        {
            Directory.CreateDirectory("Assets/Content/Materials");
            Directory.CreateDirectory("Assets/Content/Data");
            Directory.CreateDirectory(SoundsPath);
            Directory.CreateDirectory(GlyphsPath);
            Directory.CreateDirectory("Assets/Scenes");
            TextMeshProResources();
            Materials();
            Data(false);
            Sounds(false);
            GlyphFiles(false);
            AssetDatabase.Refresh();
            GlyphImport();
            Library();
            Scene();
            Settings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("TideWall setup complete.");
        }

        /// <summary>TextMeshPro's default font and settings live in Assets/TextMesh Pro (imported once from the UGUI
        /// package and committed). If they're ever missing, say how to get them back.</summary>
        static void TextMeshProResources()
        {
            if (!File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset"))
                Debug.LogError("TideWall setup: TextMeshPro resources are missing. Window > TextMeshPro > Import TMP Essential Resources.");
        }

        [UnityEditor.MenuItem("TideWall/Reset Data To Defaults")]
        public static void ResetData() { Data(true); AssetDatabase.SaveAssets(); }

        [UnityEditor.MenuItem("TideWall/Bake Sounds")]
        public static void BakeSounds() { Sounds(true); AssetDatabase.Refresh(); Library(); AssetDatabase.SaveAssets(); }

        /// <summary>SfxSynth's formulas → 16-bit mono WAVs in Assets/Content/Sounds (only missing ones, unless overwrite).</summary>
        static void Sounds(bool overwrite)
        {
            foreach (var kv in SfxSynth.All())
            {
                string path = Path.Combine(SoundsPath, kv.Key + ".wav");
                if (!overwrite && File.Exists(path)) continue;
                using var w = new BinaryWriter(File.Create(path));
                var samples = kv.Value;
                int bytes = samples.Length * 2;
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + bytes);
                w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16);
                w.Write((short)1); w.Write((short)1); w.Write(SfxSynth.Rate); w.Write(SfxSynth.Rate * 2); w.Write((short)2); w.Write((short)16);
                w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(bytes);
                foreach (var f in samples) w.Write((short)Mathf.RoundToInt(Mathf.Clamp(f, -1, 1) * 32767));
            }
        }

        [UnityEditor.MenuItem("TideWall/Redraw Glyphs")]
        public static void RedrawGlyphs() { GlyphFiles(true); AssetDatabase.Refresh(); GlyphImport(); Library(); AssetDatabase.SaveAssets(); }

        /// <summary>The symbols (GlyphArt) → PNGs in Assets/Content/Glyphs (only missing ones, unless overwrite).</summary>
        static void GlyphFiles(bool overwrite)
        {
            foreach (var name in GlyphArt.Names)
            {
                string path = Path.Combine(GlyphsPath, name + ".png");
                if (!overwrite && File.Exists(path)) continue;
                var tex = GlyphArt.Texture(name);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
        }

        static void GlyphImport()
        {
            foreach (var name in GlyphArt.Names)
            {
                var ti = AssetImporter.GetAtPath($"{GlyphsPath}/{name}.png") as TextureImporter;
                if (ti == null || (ti.alphaIsTransparency && ti.wrapMode == TextureWrapMode.Clamp)) continue;
                ti.textureType = TextureImporterType.Default;
                ti.alphaIsTransparency = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.mipmapEnabled = true;
                ti.SaveAndReimport();
            }
        }

        /// <summary>The content library: the data, base materials, and every model and sound in Assets/Content.</summary>
        static void Library()
        {
            var lib = AssetDatabase.LoadAssetAtPath<ContentLibrary>(LibraryPath);
            if (lib == null) { lib = ScriptableObject.CreateInstance<ContentLibrary>(); AssetDatabase.CreateAsset(lib, LibraryPath); }
            lib.database = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Content/Data/GameDatabase.asset");
            lib.phases = AssetDatabase.LoadAssetAtPath<PhaseCycle>("Assets/Content/Data/PhaseCycle.asset");
            Material M(string n) => AssetDatabase.LoadAssetAtPath<Material>($"Assets/Content/Materials/{n}.mat");
            lib.lit = M("Lit"); lib.litTransparent = M("LitTransparent"); lib.unlit = M("Unlit");
            lib.particle = M("Particle"); lib.particleAdditive = M("ParticleAdditive"); lib.sky = M("Sky");
            lib.models = new List<ContentLibrary.NamedModel>();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Content/Models" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                lib.models.Add(new ContentLibrary.NamedModel { name = Path.GetFileNameWithoutExtension(path), prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path) });
            }
            lib.glyphs = new List<ContentLibrary.NamedGlyph>();
            foreach (var name in GlyphArt.Names)
                lib.glyphs.Add(new ContentLibrary.NamedGlyph { name = name, texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{GlyphsPath}/{name}.png") });
            lib.campaign = AssetDatabase.LoadAssetAtPath<Campaign>("Assets/Content/Data/Campaign.asset");
            lib.sounds = new List<ContentLibrary.NamedSound>();
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { SoundsPath }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                lib.sounds.Add(new ContentLibrary.NamedSound { name = Path.GetFileNameWithoutExtension(path), clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path) });
            }
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Content.Use(lib);
        }

        static void Materials()
        {
            Mat("Lit", "Universal Render Pipeline/Lit", false, false);
            Mat("LitTransparent", "Universal Render Pipeline/Lit", true, false);
            Mat("Unlit", "Universal Render Pipeline/Unlit", false, false);
            Mat("Particle", "Universal Render Pipeline/Particles/Unlit", true, false);
            Mat("ParticleAdditive", "Universal Render Pipeline/Particles/Unlit", true, true);
            Mat("Sky", "Skybox/Procedural", false, false);   // the sky (tinted per phase by World.SetSky)
        }

        static void Mat(string name, string shader, bool transparent, bool additive)
        {
            string path = $"Assets/Content/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, path); }
            if (transparent)
            {
                m.SetFloat("_Surface", 1);
                m.SetFloat("_Blend", additive ? 2 : 0);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
                m.SetFloat("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", .25f);
            EditorUtility.SetDirty(m);
        }

        static void Data(bool overwrite)
        {
            const string dbPath = "Assets/Content/Data/GameDatabase.asset", cyclePath = "Assets/Content/Data/PhaseCycle.asset";
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(dbPath);
            if (db == null) { db = ScriptableObject.CreateInstance<GameDatabase>(); db.FillDefaults(); AssetDatabase.CreateAsset(db, dbPath); }
            else if (overwrite) { db.FillDefaults(); EditorUtility.SetDirty(db); }
            else
            {
                // Keep the asset's edits, but bring in creatures added to the defaults since (the Tidemother), and who
                // carries the Mark
                var fresh = ScriptableObject.CreateInstance<GameDatabase>();
                fresh.FillDefaults();
                foreach (var e in fresh.enemies)
                {
                    var have = db.enemies.Find(x => x.id == e.id);
                    if (have == null) { db.enemies.Add(e); EditorUtility.SetDirty(db); }
                    else if (e.mark && !have.mark) { have.mark = true; EditorUtility.SetDirty(db); }
                }
                // ... and gun attachments and ammo, added after the asset was made (or made before the two-slot layout: an
                // entry whose slot or mount no longer matches the defaults is replaced)
                foreach (var a in fresh.attachments)
                {
                    int i = db.attachments.FindIndex(x => x.id == a.id);
                    if (i < 0) { db.attachments.Add(a); EditorUtility.SetDirty(db); }
                    else if (db.attachments[i].slot != a.slot || db.attachments[i].mount != a.mount) { db.attachments[i] = a; EditorUtility.SetDirty(db); }
                }
                Object.DestroyImmediate(fresh);
            }
            var cycle = AssetDatabase.LoadAssetAtPath<PhaseCycle>(cyclePath);
            if (cycle == null) { cycle = ScriptableObject.CreateInstance<PhaseCycle>(); cycle.FillDefaults(); AssetDatabase.CreateAsset(cycle, cyclePath); }
            else if (overwrite) { cycle.FillDefaults(); EditorUtility.SetDirty(cycle); }
            const string campaignPath = "Assets/Content/Data/Campaign.asset";
            var campaign = AssetDatabase.LoadAssetAtPath<Campaign>(campaignPath);
            if (campaign == null) { campaign = ScriptableObject.CreateInstance<Campaign>(); campaign.FillDefaults(); AssetDatabase.CreateAsset(campaign, campaignPath); }
            else if (overwrite) { campaign.FillDefaults(); EditorUtility.SetDirty(campaign); }
        }

        /// <summary>The game's one scene: post-processing, the GameManager (with the content library) and the level.</summary>
        static void Scene()
        {
            if (!File.Exists(ScenePath)) NewScene();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            bool changed = false;
            var gm = Object.FindAnyObjectByType<GameManager>();
            if (gm == null) { gm = new GameObject("TideWall").AddComponent<GameManager>(); changed = true; }
            var so = new SerializedObject(gm);
            var content = so.FindProperty("content");
            if (content.objectReferenceValue == null) { content.objectReferenceValue = AssetDatabase.LoadAssetAtPath<ContentLibrary>(LibraryPath); so.ApplyModifiedPropertiesWithoutUndo(); changed = true; }
            if (changed) EditorSceneManager.SaveScene(scene);
            if (Object.FindAnyObjectByType<World>() == null) WorldBaker.Rebuild();
            // Part II's stages, one scene each, the first time
            if (ExpeditionBaker.ScenePaths().Exists(path => !File.Exists(path))) ExpeditionBaker.Rebuild();
        }

        static void NewScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var post = new GameObject("Post Processing");
            var vol = post.AddComponent<Volume>();
            vol.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = profile.Add<Bloom>(true); bloom.intensity.Override(.6f); bloom.threshold.Override(1.1f);
            var tone = profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
            var color = profile.Add<ColorAdjustments>(true); color.saturation.Override(12); color.postExposure.Override(.15f);
            var vig = profile.Add<Vignette>(true); vig.intensity.Override(.22f);
            AssetDatabase.CreateAsset(profile, "Assets/Settings/TideWallVolume.asset");
            vol.sharedProfile = profile;
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        /// <summary>A "HUD" sorting layer after Default: players' HUD canvases sort after everything in the world, so
        /// world text (signs, name tags) can never draw over them.</summary>
        static void SortingLayers()
        {
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tags.FindProperty("m_SortingLayers");
            for (int i = 0; i < layers.arraySize; i++)
                if (layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == "HUD") return;
            layers.InsertArrayElementAtIndex(layers.arraySize);
            var layer = layers.GetArrayElementAtIndex(layers.arraySize - 1);
            layer.FindPropertyRelative("name").stringValue = "HUD";
            layer.FindPropertyRelative("uniqueID").intValue = 0x48554401;
            tags.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>The build's scenes: the town (Part I and the menu) first, then every stage that exists.</summary>
        public static void BuildScenes()
        {
            var scenes = new List<EditorBuildSettingsScene> { new(ScenePath, true) };
            foreach (var path in ExpeditionBaker.ScenePaths()) if (File.Exists(path)) scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void Settings()
        {
            SortingLayers();
            BuildScenes();
            PlayerSettings.productName = "TideWall";
            PlayerSettings.companyName = "CheeseyDanish";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
        }
    }

    /// <summary>Import rules for the Blender models: no cameras, lights or animation; keep flat-shaded normals.</summary>
    public class ModelImport : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/Content/Models")) return;
            var mi = (ModelImporter)assetImporter;
            mi.importAnimation = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.importNormals = ModelImporterNormals.Import;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            mi.useFileScale = true;
            mi.isReadable = false;
        }
    }
}
