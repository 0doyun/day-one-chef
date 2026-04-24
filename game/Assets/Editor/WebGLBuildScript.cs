// Headless WebGL build for the OSS_IME_Probe scene.
// Invoked by scripts/build-webgl-probe.sh via Unity -batchmode -executeMethod.
//
// Compression: Unity defaults to Brotli, but Chrome/Safari/Firefox refuse
// to decode `Content-Encoding: br` over plain HTTP — only HTTPS. For the
// local probe loop we force Gzip, which all browsers decode over HTTP.
// Production deploy (Vercel) can flip back to Brotli.

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
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
        private const string MainProdOutputDir = "Build/webgl-main-prod";

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

        /// <summary>
        /// Production WebGL build — same scene + settings as
        /// <see cref="BuildWebGLMain"/>, but compressed with Brotli and
        /// no decompression fallback. Brotli beats Gzip by roughly
        /// 15 – 20 % on Unity's WASM + data payload and lands the build
        /// near the ≤ 15 MB GDD budget. Requires HTTPS delivery so
        /// browsers accept `Content-Encoding: br` — use this for Vercel
        /// / TestFlight builds, not for `python3 -m http.server` loops.
        /// </summary>
        public static void BuildWebGLMainProd()
        {
            ApplyCommonWebGLSettings();
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = false;

            KoreanFontSetup.InstallKoreanFont();
            MainKitchenSetup.Setup();

            RunBuild(MainScenePath, MainProdOutputDir);
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

            // Size optimisation knobs (GDD performance budget: ≤ 15 MB
            // gzipped). `stripEngineCode` drops unused engine modules
            // from the WASM binary; `Medium` managed stripping runs
            // IL2CPP's dead-code pass without breaking TMP reflection.
            // `High` strips more but has tripped TMP_FontAsset material
            // construction in past projects — raise to High only with a
            // link.xml once we have a working baseline.
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium);

            // Custom HTML shell — Unity's default template ships a
            // fullscreen button, progress bar image, and Unity branding
            // that add ~40 KB of template overhead and don't suit the
            // eventual Flutter WebView host. Project template is a
            // minimal canvas + loading text.
            PlayerSettings.WebGL.template = "PROJECT:DayOneChef";
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

            if (summary.result == BuildResult.Succeeded)
            {
                LogOutputSizeBreakdown(outputDir);
            }
            else
            {
                EditorApplication.Exit(1);
            }
        }

        private static void LogOutputSizeBreakdown(string outputDir)
        {
            if (!Directory.Exists(outputDir))
            {
                Debug.LogWarning($"[WebGLBuildScript] Output dir missing: {outputDir}");
                return;
            }

            long total = 0;
            long buildBytes = 0;
            long templateBytes = 0;
            foreach (var file in Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories))
            {
                var size = new FileInfo(file).Length;
                total += size;
                var rel = Path.GetRelativePath(outputDir, file);
                if (rel.StartsWith("Build")) buildBytes += size;
                else templateBytes += size;
            }

            Debug.Log(
                $"[WebGLBuildScript] Size report — total={Format(total)} " +
                $"Build/={Format(buildBytes)} template+other={Format(templateBytes)} " +
                $"(budget: ≤ 15 MB gzipped per technical-preferences.md)");
        }

        private static string Format(long bytes)
        {
            if (bytes > 1024L * 1024) return $"{bytes / 1024.0 / 1024.0:F2} MB";
            if (bytes > 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes} B";
        }
    }
}
#endif
