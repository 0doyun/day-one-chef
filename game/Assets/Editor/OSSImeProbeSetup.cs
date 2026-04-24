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
        private const int CharacterLimit = 80;

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
            inputField.characterLimit = CharacterLimit;
            inputField.contentType = TMP_InputField.ContentType.Standard;
            inputField.lineType = TMP_InputField.LineType.SingleLine;

            if (inputField.placeholder is TextMeshProUGUI placeholderText)
            {
                placeholderText.text = Placeholder;
            }

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
