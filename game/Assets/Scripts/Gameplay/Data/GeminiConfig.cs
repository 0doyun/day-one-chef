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
        // `gemini-2.5-flash` is the plague-magnet free-tier default and
        // returns sustained HTTP 503 ("This model is currently experiencing
        // high demand") on the free quota. `-lite` is the same family, a
        // cheaper/faster variant, and has far better free-tier
        // availability while being more than enough quality for our
        // instruction→action→monologue shape. Override in the .asset
        // Inspector if paid quota or a different tier is in use.
        [SerializeField] private string _model = "gemini-2.5-flash-lite";

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
    }
}
