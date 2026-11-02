// TideWall 3D — bakes The Last Light's level into its scene (like ExpeditionBaker does for Part II's stages).
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace TideWall.EditorTools
{
    public static class LighthouseBaker
    {
        const string Generated = "Assets/Content/GeneratedLastLight";
        public static string ScenePath => $"Assets/Scenes/{Lighthouse.SceneName}.unity";

        [UnityEditor.MenuItem("TideWall/Rebuild The Last Light")]
        public static void Rebuild()
        {
            if (AssetDatabase.IsValidFolder(Generated)) AssetDatabase.DeleteAsset(Generated);
            Directory.CreateDirectory(Generated + "/Meshes");
            Directory.CreateDirectory(Generated + "/Materials");
            AssetDatabase.Refresh();
            var library = AssetDatabase.LoadAssetAtPath<ContentLibrary>(TideWallSetup.LibraryPath);
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/TideWallVolume.asset");
            Content.Use(library);
            var instantiate = ModelFactory.Instantiate;
            ModelFactory.Instantiate = (prefab, parent) => (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var post = new GameObject("Post Processing");
                var vol = post.AddComponent<Volume>();
                vol.isGlobal = true;
                vol.sharedProfile = profile;
                var gm = new GameObject("TideWall").AddComponent<GameManager>();
                var so = new SerializedObject(gm);
                so.FindProperty("content").objectReferenceValue = library;
                so.ApplyModifiedPropertiesWithoutUndo();
                var level = LighthouseBuilder.Build(null);
                WorldBaker.Persist(level.gameObject, Generated);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            finally { ModelFactory.Instantiate = instantiate; }
            AssetDatabase.SaveAssets();
            TideWallSetup.BuildScenes();
            EditorSceneManager.OpenScene(TideWallSetup.ScenePath, OpenSceneMode.Single);
            Debug.Log("TideWall: The Last Light rebuilt.");
        }
    }
}
