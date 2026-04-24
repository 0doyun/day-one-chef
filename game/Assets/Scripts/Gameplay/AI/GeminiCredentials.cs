// Runtime / editor-time API key lookup. Centralised so the rest of the
// code never touches PlayerPrefs or EditorPrefs directly — keeps the
// call sites short and the policy auditable.
//
// The key itself is NEVER committed to the repo:
//   - In the Editor, it lives in EditorPrefs (per-machine, not per-project).
//   - In a player build, it lives in PlayerPrefs (per-machine, per-user).
//   - Production WebGL/Flutter builds should eventually bypass this
//     class entirely and proxy through Flutter — see ADR-0003.

using UnityEngine;

namespace DayOneChef.Gameplay.AI
{
    public static class GeminiCredentials
    {
        public const string EditorPrefKey = "DayOneChef.Gemini.ApiKey";
        public const string RuntimePrefKey = "DayOneChef.Gemini.ApiKey";

        /// <summary>
        /// Returns the stored API key, or empty string if nothing set.
        /// </summary>
        public static string GetApiKey()
        {
#if UNITY_EDITOR
            return UnityEditor.EditorPrefs.GetString(EditorPrefKey, string.Empty);
#else
            return PlayerPrefs.GetString(RuntimePrefKey, string.Empty);
#endif
        }

        public static void SetApiKey(string key)
        {
            var trimmed = key?.Trim() ?? string.Empty;
#if UNITY_EDITOR
            UnityEditor.EditorPrefs.SetString(EditorPrefKey, trimmed);
#else
            PlayerPrefs.SetString(RuntimePrefKey, trimmed);
            PlayerPrefs.Save();
#endif
        }

        public static bool HasKey() => !string.IsNullOrWhiteSpace(GetApiKey());
    }
}
