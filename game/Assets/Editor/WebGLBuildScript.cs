// Headless WebGL build for the OSS_IME_Probe scene.
// Invoked by scripts/build-webgl-probe.sh via Unity -batchmode -executeMethod.
//
// Compression: Unity defaults to Brotli, but Chrome/Safari/Firefox refuse
// to decode `Content-Encoding: br` over plain HTTP — only HTTPS. For the
// local probe loop we force Gzip, which all browsers decode over HTTP.
// Production deploy (Vercel) can flip back to Brotli.

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DayOneChef.Editor
{
    public static class WebGLBuildScript
    {
        private const string ProbeScenePath = "Assets/Scenes/OSS_IME_Probe.unity";
        private const string ProbeOutputDir = "Build/webgl-ime-probe";
        private const string MainScenePath = "Assets/Scenes/MainKitchen.unity";
        private const string MainOutputDir = "Build/webgl-main";

        public static void BuildWebGL()
        {
            ApplyCommonWebGLSettings();

            // Ensure the Korean font asset exists and is baked to a static
            // atlas before the probe scene references it.
            KoreanFontSetup.InstallKoreanFont();

            // Re-apply the Korean font + probe settings to the committed
            // scene. Batch-mode Unity can't use ExecuteMenuItem for
            // "GameObject/UI/Input Field - TextMeshPro", so full scene
            // regeneration is interactive-only (via Tools → Day One Chef →
            // Setup OSS IME Probe). The build path only patches an
            // existing scene.
            OSSImeProbeSetup.ApplyFontAndSave();

            RunBuild(ProbeScenePath, ProbeOutputDir);
        }

        public static void BuildWebGLMain()
        {
            ApplyCommonWebGLSettings();

            // MainKitchenSetup is fully code-driven (no ExecuteMenuItem),
            // so we always regenerate the scene in batch mode. The scene
            // and placeholder sprite are gitignored derived artifacts.
            KoreanFontSetup.InstallKoreanFont();
            MainKitchenSetup.Setup();

            RunBuild(MainScenePath, MainOutputDir);
        }

        private static void ApplyCommonWebGLSettings()
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;

            // Raise the initial WASM heap to 512 MB. The default 256 MB
            // leaves too little headroom once the Korean SDF atlases and
            // TMP subsystems are loaded; probe iteration 1 aborted with
            // RuntimeError: 67133512 on first keystroke. 512 MB is
            // well-supported by current browsers and still within
            // mobile-WKWebView comfort.
            PlayerSettings.WebGL.memorySize = 512;
        }

        private static void RunBuild(string scenePath, string outputDir)
        {
            var options = new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = outputDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            Debug.Log(
                $"[WebGLBuildScript] scene={scenePath} result={summary.result} " +
                $"compression={PlayerSettings.WebGL.compressionFormat} " +
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
