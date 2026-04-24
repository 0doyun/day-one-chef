// Headless WebGL build for the OSS_IME_Probe scene.
// Invoked by scripts/build-webgl-probe.sh via Unity -batchmode -executeMethod.

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DayOneChef.Editor
{
    public static class WebGLBuildScript
    {
        private const string ScenePath = "Assets/Scenes/OSS_IME_Probe.unity";
        private const string OutputDir = "Build/webgl-ime-probe";

        public static void BuildWebGL()
        {
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = OutputDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            Debug.Log(
                $"[WebGLBuildScript] result={summary.result} " +
                $"totalTimeMs={summary.totalTime.TotalMilliseconds:F0} " +
                $"totalSize={summary.totalSize} " +
                $"errors={summary.totalErrors} warnings={summary.totalWarnings}");

            if (summary.result != BuildResult.Succeeded)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
#endif
