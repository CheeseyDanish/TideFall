// TideWall 3D — headless builds:
//   unity build <project> --target StandaloneWindows64 --execute-method TideWall.EditorTools.BuildScript.Windows
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TideWall.EditorTools
{
    public static class BuildScript
    {
        [UnityEditor.MenuItem("TideWall/Build Windows")]
        public static void Windows()
        {
            TideWallSetup.Run();
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var report = BuildPipeline.BuildPlayer(scenes, "Builds/Windows/TideWall.exe", BuildTarget.StandaloneWindows64, BuildOptions.None);
            Debug.Log($"TideWall build: {report.summary.result}, {report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalErrors} errors");
            if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
