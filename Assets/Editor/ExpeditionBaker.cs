// TideWall 3D — generates Part II's stages, one scene each (menu: TideWall → Rebuild Stages). Each scene gets
// post-processing, a GameManager with the content library, and the stage from ExpeditionBuilder, with every model
// kept linked to its asset and anything made in code saved under Assets/Content/GeneratedStages. The scenes are named
// by the campaign (StageDef.scene) and added to the build. After that, edit them in Unity; rebuilding replaces them.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace TideWall.EditorTools
{
    public static class ExpeditionBaker
    {
        const string Generated = "Assets/Content/GeneratedStages";

        [UnityEditor.MenuItem("TideWall/Rebuild Stages")]
        static void RebuildMenu()
        {
            if (EditorUtility.DisplayDialog("Rebuild Part II's stages?", "This regenerates every stage scene from ExpeditionBuilder, replacing any changes made to them by hand.", "Rebuild", "Cancel"))
                Rebuild();
        }

        /// <summary>The scenes of the campaign's assault parts, in order (as the build lists them).</summary>
        public static List<string> ScenePaths()
        {
            var list = new List<string>();
            var campaign = AssetDatabase.LoadAssetAtPath<Campaign>("Assets/Content/Data/Campaign.asset");
            if (campaign == null) return list;
            foreach (var part in campaign.parts)
                if (part.kind == PartKind.Assault)
                    foreach (var st in part.stages) list.Add($"Assets/Scenes/{st.scene}.unity");
            return list;
        }

        public static void Rebuild()
        {
            var campaign = AssetDatabase.LoadAssetAtPath<Campaign>("Assets/Content/Data/Campaign.asset");
            var part = campaign != null ? campaign.parts.Find(p => p.kind == PartKind.Assault) : null;
            if (part == null) { Debug.LogError("TideWall: the campaign has no assault part to build stages for."); return; }
            if (AssetDatabase.IsValidFolder(Generated)) AssetDatabase.DeleteAsset(Generated);
            AssetDatabase.Refresh();

            var library = AssetDatabase.LoadAssetAtPath<ContentLibrary>(TideWallSetup.LibraryPath);
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/TideWallVolume.asset");
            Content.Use(library);
            var instantiate = ModelFactory.Instantiate;
            ModelFactory.Instantiate = (prefab, parent) => (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            try
            {
                for (int i = 0; i < part.stages.Count; i++)
                {
                    string folder = $"{Generated}/{i + 1}";
                    Directory.CreateDirectory(folder + "/Meshes");
                    Directory.CreateDirectory(folder + "/Materials");
                    AssetDatabase.Refresh();

                    var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    var post = new GameObject("Post Processing");
                    var vol = post.AddComponent<Volume>();
                    vol.isGlobal = true;
                    vol.sharedProfile = profile;
                    var gm = new GameObject("TideWall").AddComponent<GameManager>();
                    var so = new SerializedObject(gm);
                    so.FindProperty("content").objectReferenceValue = library;
                    so.ApplyModifiedPropertiesWithoutUndo();

                    var ex = ExpeditionBuilder.Build(null, i);
                    WorldBaker.Persist(ex.gameObject, folder);
                    EditorSceneManager.SaveScene(scene, $"Assets/Scenes/{part.stages[i].scene}.unity");
                }
            }
            finally { ModelFactory.Instantiate = instantiate; }
            AssetDatabase.SaveAssets();
            TideWallSetup.BuildScenes();
            EditorSceneManager.OpenScene(TideWallSetup.ScenePath, OpenSceneMode.Single);
            Debug.Log($"TideWall: {part.stages.Count} stages rebuilt.");
        }
    }
}
