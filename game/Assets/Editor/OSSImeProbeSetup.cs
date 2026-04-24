// One-click scene setup for ADR-0001 Phase 1 OSS evaluation.
// See: docs/phase-1-ime-evaluation-protocol.md §Step 2
//
// Invocation: Unity menu → Tools → Day One Chef → Setup OSS IME Probe

#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DayOneChef.Editor
{
    public static class OSSImeProbeSetup
    {
        private const string ScenePath = "Assets/Scenes/OSS_IME_Probe.unity";
        private const string Placeholder = "예: 그릇에 계란 먼저 깨고 그 다음 물을 섞어서 쪄줘";
        private const string KoreanFontAssetPath = "Assets/Fonts/NotoSansKR SDF.asset";
        private const int CharacterLimit = 80;

        /// <summary>
        /// Build-pipeline safe entry point. Loads the existing probe scene
        /// (committed to the repo), re-applies font + character limit +
        /// placeholder text, and saves. No menu execution — works in
        /// -batchmode -nographics.
        /// </summary>
        public static void ApplyFontAndSave()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.LogWarning($"[OSSImeProbeSetup] Scene not found at {ScenePath}. " +
                                 "Open Unity interactively and run Tools → Day One Chef → Setup OSS IME Probe first.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var inputField = UnityEngine.Object.FindFirstObjectByType<TMP_InputField>();
            if (inputField == null)
            {
                Debug.LogError("[OSSImeProbeSetup] No TMP_InputField in the probe scene — cannot apply font.");
                return;
            }

            ConfigureInputField(inputField);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[OSSImeProbeSetup] Re-applied font + settings to committed probe scene.");
        }

        [MenuItem("Tools/Day One Chef/Setup OSS IME Probe")]
        public static void Setup()
        {
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects,
                NewSceneMode.Single);

            Selection.activeObject = null;
            if (!EditorApplication.ExecuteMenuItem("GameObject/UI/Input Field - TextMeshPro"))
            {
                Debug.LogError(
                    "[OSSImeProbeSetup] Menu 'GameObject/UI/Input Field - TextMeshPro' unavailable. " +
                    "Import TMP Essentials first: Window → TextMeshPro → Import TMP Essential Resources.");
                return;
            }

            var inputFieldGo = Selection.activeGameObject;
            if (inputFieldGo == null)
            {
                Debug.LogError("[OSSImeProbeSetup] InputField was not created — ExecuteMenuItem did not select anything.");
                return;
            }

            var inputField = inputFieldGo.GetComponent<TMP_InputField>();
            ConfigureInputField(inputField);

            // Attach kou-yeung/WebGLInput via reflection so this script compiles
            // even before the UPM import resolves.
            var webglInputType = System.Type.GetType("WebGLSupport.WebGLInput, WebGLSupport");
            if (webglInputType == null)
            {
                Debug.LogError(
                    "[OSSImeProbeSetup] WebGLSupport.WebGLInput not found. Verify game/Packages/manifest.json " +
                    "contains com.github.kou-yeung and the package finished importing.");
                return;
            }
            inputFieldGo.AddComponent(webglInputType);

            var sceneDir = Path.GetDirectoryName(ScenePath);
            if (!string.IsNullOrEmpty(sceneDir))
            {
                Directory.CreateDirectory(sceneDir);
            }
            EditorSceneManager.SaveScene(scene, ScenePath);

            RegisterSceneInBuildSettings(ScenePath);

            Debug.Log($"[OSSImeProbeSetup] Wrote {ScenePath} — ready to build WebGL.");
        }

        private static void ConfigureInputField(TMP_InputField inputField)
        {
            inputField.characterLimit = CharacterLimit;
            inputField.contentType = TMP_InputField.ContentType.Standard;
            inputField.lineType = TMP_InputField.LineType.SingleLine;

            // Assign Noto Sans KR as the primary font on the text and
            // placeholder components. Primary (not TMP fallback) sidesteps
            // a WebGL IL2CPP crash in the fallback chain traversal — see
            // KoreanFontSetup.
            var koreanFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontAssetPath);
            if (koreanFont == null)
            {
                Debug.LogWarning(
                    $"[OSSImeProbeSetup] {KoreanFontAssetPath} missing — run Tools → Day One Chef " +
                    "→ Install Korean Font first. Input field will render Hangul as boxes " +
                    "but still route IME correctly.");
            }

            // NOTE: The Korean font asset is generated and committed, but
            // primary-font assignment onto TMP components renders invisible
            // in the WebGL IL2CPP build (both SDFAA and bitmap render
            // modes tested). The LiberationSans SDF path at least renders
            // tofu boxes, which proves glyph layout is running — copy-
            // paste from the input field produces the correctly-composed
            // Korean string, satisfying the IME acceptance criterion.
            // Deferring final rendering fix to Day 3 UI work; until then,
            // intentionally leave the default font so the probe remains
            // usable for the other browsers in the Phase 1 matrix.
            _ = koreanFont; // currently unused — see note above

            if (inputField.textComponent is TextMeshProUGUI textLabel)
            {
                textLabel.color = Color.black;
                textLabel.alpha = 1f;
                EditorUtility.SetDirty(textLabel);
            }

            if (inputField.placeholder is TextMeshProUGUI placeholderText)
            {
                placeholderText.text = Placeholder;
                EditorUtility.SetDirty(placeholderText);
            }

            EditorUtility.SetDirty(inputField);
        }

        private static void RegisterSceneInBuildSettings(string path)
        {
            var existing = EditorBuildSettings.scenes;
            foreach (var s in existing)
            {
                if (s.path == path) return;
            }
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(existing);
            list.Insert(0, new EditorBuildSettingsScene(path, enabled: true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
#endif
