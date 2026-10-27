// TideWall 3D — generates the level into the TideWall scene (menu: TideWall → Rebuild World In Scene).
// Runs WorldBuilder in the editor, keeps every model linked to its model asset, and saves anything made in code
// (ground and sea meshes, tinted materials) as assets under Assets/Content/Generated, so the scene is complete on its
// own. After that the scene is the level: move, add or delete things in Unity and save as usual. Rebuilding throws
// those edits away, so it asks first.
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TideWall.EditorTools
{
    public static class WorldBaker
    {
        const string Generated = "Assets/Content/Generated";

        [UnityEditor.MenuItem("TideWall/Rebuild World In Scene")]
        static void RebuildMenu()
        {
            if (EditorUtility.DisplayDialog("Rebuild the world?", "This regenerates the level in the TideWall scene from WorldBuilder, replacing any changes made to it by hand.", "Rebuild", "Cancel"))
                Rebuild();
        }

        public static void Rebuild()
        {
            var scene = EditorSceneManager.OpenScene(TideWallSetup.ScenePath, OpenSceneMode.Single);
            foreach (var old in Object.FindObjectsByType<World>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Object.DestroyImmediate(old.gameObject);
            if (AssetDatabase.IsValidFolder(Generated)) AssetDatabase.DeleteAsset(Generated);
            Directory.CreateDirectory(Generated + "/Meshes");
            Directory.CreateDirectory(Generated + "/Materials");
            AssetDatabase.Refresh();

            var instantiate = ModelFactory.Instantiate;
            ModelFactory.Instantiate = (prefab, parent) => (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            World world;
            try { world = WorldBuilder.Build(null); }
            finally { ModelFactory.Instantiate = instantiate; }

            Persist(world.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("TideWall: world rebuilt into the scene.");
        }

        /// <summary>Save every mesh and material that only exists in memory as an asset, so the scene keeps it.</summary>
        static void Persist(GameObject root)
        {
            var saved = new HashSet<Object>();
            var names = new HashSet<string>();
            string Unique(string folder, string name, string ext)
            {
                foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
                string path = $"{folder}/{name}.{ext}";
                for (int i = 2; names.Contains(path); i++) path = $"{folder}/{name} {i}.{ext}";
                names.Add(path);
                return path;
            }

            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.GetComponent<TMP_Text>() != null) continue;   // TextMeshPro rebuilds its own mesh
                var mesh = mf.sharedMesh;
                if (mesh == null || EditorUtility.IsPersistent(mesh) || !saved.Add(mesh)) continue;
                AssetDatabase.CreateAsset(mesh, Unique(Generated + "/Meshes", mesh.name.Length > 0 ? mesh.name : mf.name, "asset"));
            }
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r.GetComponent<TMP_Text>() != null) continue;
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null || EditorUtility.IsPersistent(m) || !saved.Add(m)) continue;
                    AssetDatabase.CreateAsset(m, Unique(Generated + "/Materials", m.name.Length > 0 ? m.name : r.name, "mat"));
                }
            }
        }
    }
}
