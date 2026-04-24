// Editor menu to manage the Gemini API key without committing it.
// Key is stored in EditorPrefs (per-machine, not per-project) so
// nothing related to the key ever touches the repo. See ADR-0003.
//
// Entries:
//   Tools → Day One Chef → Set Gemini API Key…  — prompt + save
//   Tools → Day One Chef → Clear Gemini API Key — nukes the EditorPref
//   Tools → Day One Chef → Check Gemini API Key — prints masked summary

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using DayOneChef.Gameplay.AI;

namespace DayOneChef.Editor
{
    public class GeminiApiKeySetup : EditorWindow
    {
        private string _input = string.Empty;
        private bool _reveal;

        [MenuItem("Tools/Day One Chef/Set Gemini API Key…")]
        public static void OpenWindow()
        {
            var window = GetWindow<GeminiApiKeySetup>(utility: true, title: "Gemini API Key");
            window.minSize = new Vector2(420, 140);
            window._input = GeminiCredentials.GetApiKey();
            window.ShowModalUtility();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Stored in EditorPrefs (per-machine). Not committed to the repo. " +
                "See ADR-0003 for why direct-from-Unity keys are prototype-only.",
                MessageType.Info);
            _reveal = EditorGUILayout.Toggle("Reveal", _reveal);
            if (_reveal)
            {
                _input = EditorGUILayout.TextField("API Key", _input);
            }
            else
            {
                _input = EditorGUILayout.PasswordField("API Key", _input);
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save"))
                {
                    GeminiCredentials.SetApiKey(_input);
                    Debug.Log("[GeminiApiKeySetup] API key saved to EditorPrefs.");
                    Close();
                }
                if (GUILayout.Button("Cancel"))
                {
                    Close();
                }
            }
        }

        [MenuItem("Tools/Day One Chef/Clear Gemini API Key")]
        public static void ClearKey()
        {
            if (EditorUtility.DisplayDialog(
                "Clear Gemini API Key?",
                "Remove the stored Gemini API key from EditorPrefs on this machine?",
                "Clear", "Cancel"))
            {
                GeminiCredentials.SetApiKey(string.Empty);
                Debug.Log("[GeminiApiKeySetup] API key cleared.");
            }
        }

        [MenuItem("Tools/Day One Chef/Check Gemini API Key")]
        public static void CheckKey()
        {
            var key = GeminiCredentials.GetApiKey();
            if (string.IsNullOrEmpty(key))
            {
                Debug.Log("[GeminiApiKeySetup] No API key configured. " +
                          "Use Tools → Day One Chef → Set Gemini API Key…");
                return;
            }
            var masked = key.Length <= 8
                ? new string('*', key.Length)
                : $"{key.Substring(0, 4)}…{key.Substring(key.Length - 4)}";
            Debug.Log($"[GeminiApiKeySetup] API key present (masked: {masked}, length {key.Length}).");
        }
    }
}
#endif
