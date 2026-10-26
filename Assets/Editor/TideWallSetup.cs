// TideWall 3D — one-click project setup (menu: TideWall → Setup Project, or headless:
//   Unity -batchmode -projectPath . -executeMethod TideWall.EditorTools.TideWallSetup.Run -quit).
// Idempotent: creates whatever is missing — base materials, data assets, the game scene with post-processing,
// and build/player settings. "Reset Data" rewrites the data assets from the code defaults.
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
        const string ScenePath = "Assets/Scenes/TideWall.unity";

        [UnityEditor.MenuItem("TideWall/Setup Project")]
        public static void Run()
        {
            Directory.CreateDirectory("Assets/Resources/Materials");
            Directory.CreateDirectory("Assets/Resources/Data");
            Directory.CreateDirectory("Assets/Scenes");
            Materials();
            Data(false);
            Scene();
            Settings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("TideWall setup complete.");
        }

        [UnityEditor.MenuItem("TideWall/Reset Data To Defaults")]
        public static void ResetData() { Data(true); AssetDatabase.SaveAssets(); }

        static void Materials()
        {
            Mat("Lit", "Universal Render Pipeline/Lit", false, false);
            Mat("LitTransparent", "Universal Render Pipeline/Lit", true, false);
            Mat("Unlit", "Universal Render Pipeline/Unlit", false, false);
            Mat("Particle", "Universal Render Pipeline/Particles/Unlit", true, false);
            Mat("ParticleAdditive", "Universal Render Pipeline/Particles/Unlit", true, true);
        }

        static void Mat(string name, string shader, bool transparent, bool additive)
        {
            string path = $"Assets/Resources/Materials/{name}.mat";
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
            const string dbPath = "Assets/Resources/Data/GameDatabase.asset", cyclePath = "Assets/Resources/Data/PhaseCycle.asset";
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(dbPath);
            if (db == null) { db = ScriptableObject.CreateInstance<GameDatabase>(); db.FillDefaults(); AssetDatabase.CreateAsset(db, dbPath); }
            else if (overwrite) { db.FillDefaults(); EditorUtility.SetDirty(db); }
            var cycle = AssetDatabase.LoadAssetAtPath<PhaseCycle>(cyclePath);
            if (cycle == null) { cycle = ScriptableObject.CreateInstance<PhaseCycle>(); cycle.FillDefaults(); AssetDatabase.CreateAsset(cycle, cyclePath); }
            else if (overwrite) { cycle.FillDefaults(); EditorUtility.SetDirty(cycle); }
        }

        static void Scene()
        {
            if (File.Exists(ScenePath)) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Everything else is built at runtime by GameManager (it boots itself). The scene only holds post-processing.
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

        static void Settings()
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
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
            if (!assetPath.StartsWith("Assets/Resources/Models")) return;
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
