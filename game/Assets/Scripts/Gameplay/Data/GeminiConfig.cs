// Gemini call configuration. Committable — API key is intentionally
// NOT stored here and is pulled from EditorPrefs / PlayerPrefs at
// runtime via GeminiCredentials. See ADR-0003.

using UnityEngine;

namespace DayOneChef.Gameplay.Data
{
    [CreateAssetMenu(
        menuName = "Day One Chef/Gemini Config",
        fileName = "GeminiConfig")]
    public class GeminiConfig : ScriptableObject
    {
        // Day 13-B: settled on `gemini-2.5-flash`. The earlier hops
        // (`gemini-2.5-flash-lite` → `gemini-2.0-flash`) failed for
        // unrelated reasons: lite tier exhausted 250 RPD during
        // 5-round playtests, and 2.0-flash was silently deprecated for
        // new users (Google returned `429 limit:0 FREE_TIER` instead of
        // the actual 404, which sent us down a billing rabbit hole).
        // 2.5-flash is the current Gemini Flash model and works on
        // both free and paid tier; override in the .asset Inspector if
        // a different model is needed.
        [SerializeField] private string _model = "gemini-2.5-flash";

        [Tooltip("Base URL of the generateContent endpoint. No trailing slash; " +
                 "no `?key=…` — the key is appended at request time.")]
        [SerializeField] private string _endpoint =
            "https://generativelanguage.googleapis.com/v1beta/models";

        [Tooltip("GDD §16 tuning: call #1 should stay at ~0.7 so the chef " +
                 "emits plausible-but-literal execution of the player's 지시.")]
        [SerializeField, Range(0f, 1.5f)] private float _temperature = 0.7f;

        [Tooltip("GDD §13: GEMINI_TIMEOUT. 8 seconds matches the spec.")]
        [SerializeField, Range(1, 30)] private int _timeoutSeconds = 8;

        [Tooltip("GDD §13: GEMINI_RETRY. 1 retry on timeout before falling back " +
                 "to the 'chef frozen' failure mode in §4.3.")]
        [SerializeField, Range(0, 3)] private int _retries = 1;

        public string Model => _model;
        public string Endpoint => _endpoint;
        public float Temperature => _temperature;
        public int TimeoutSeconds => _timeoutSeconds;
        public int Retries => _retries;

        public string BuildGenerateContentUrl(string apiKey)
        {
            return $"{_endpoint}/{_model}:generateContent?key={apiKey}";
        }

        /// <summary>
        /// Same-origin relative path served by the Flutter shell's shelf
        /// proxy (`app/lib/src/shell/gemini_proxy.dart`). The Unity WebGL
        /// build never holds the API key: it POSTs the Gemini request
        /// body here and the proxy adds the `x-goog-api-key` header from
        /// the Dart-side `.env`. See ADR-0003 Phase B.
        /// </summary>
        public string BuildProxyUrl()
        {
            return $"/api/gemini/{_model}:generateContent";
        }
    }
}
