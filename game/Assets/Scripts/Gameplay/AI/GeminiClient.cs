// Direct-from-Unity Gemini call #1 implementation. See ADR-0003 for
// why this ships for Day 5 despite embedding the API key in the client:
// short version, the Flutter proxy doesn't land until Day 8-9, and an
// IGeminiClient interface lets us swap without touching gameplay code.
//
// Uses UnityEngine's JsonUtility for parsing — Newtonsoft would give
// us richer error messages on schema drift, but adds a Package
// Manager dependency that Day 5 doesn't need yet. If the evaluator
// on Day 11 outgrows JsonUtility we can add Newtonsoft then.

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using DayOneChef.Gameplay.Data;

namespace DayOneChef.Gameplay.AI
{
    public class GeminiCallException : Exception
    {
        public GeminiCallException(string message) : base(message) { }
        public GeminiCallException(string message, Exception inner) : base(message, inner) { }
    }

    public class GeminiClient : IGeminiClient
    {
        private readonly GeminiConfig _config;
        private readonly Func<string> _apiKeyProvider;

        public GeminiClient(GeminiConfig config, Func<string> apiKeyProvider = null)
        {
            _config = config != null ? config : throw new ArgumentNullException(nameof(config));
            _apiKeyProvider = apiKeyProvider ?? GeminiCredentials.GetApiKey;
        }

        public async Task<ChefActionResponse> GenerateActionsAsync(
            GameStateSnapshot state,
            string playerInstruction,
            CancellationToken ct = default)
        {
            // ADR-0003 Phase B: in a shipped WebGL build the API key
            // stays on the Dart side. The Flutter shell's shelf server
            // hosts `/api/gemini/{model}:generateContent` and injects
            // the key from `.env`. Editor play keeps the Day 5 direct-
            // from-Unity path so the offline/Editor loop still works
            // without booting the Flutter host.
#if UNITY_WEBGL && !UNITY_EDITOR
            const bool useProxy = true;
            string apiKey = null;
#else
            const bool useProxy = false;
            var apiKey = _apiKeyProvider();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new GeminiCallException(
                    "No API key configured. Open Tools → Day One Chef → Set Gemini API Key in the editor, " +
                    "or call GeminiCredentials.SetApiKey(key) at runtime.");
            }
#endif

            var systemPrompt = GeminiPromptBuilder.BuildSystemPrompt();
            var userPrompt = GeminiPromptBuilder.BuildUserPrompt(state, playerInstruction);
            var requestBody = BuildRequestJson(systemPrompt, userPrompt);

            string responseText;
            Exception lastError = null;
            for (var attempt = 0; attempt <= _config.Retries; attempt++)
            {
                try
                {
                    responseText = await SendOnceAsync(apiKey, requestBody, ct, useProxy).ConfigureAwait(true);
                    return ParseResponse(responseText);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    lastError = ex;
                    Debug.LogWarning($"[GeminiClient] Attempt {attempt + 1} failed: {ex.Message}");
                }
            }
            throw new GeminiCallException(
                $"Gemini call failed after {_config.Retries + 1} attempt(s).", lastError);
        }

        private async Task<string> SendOnceAsync(string apiKey, string body, CancellationToken ct, bool useProxy)
        {
            var url = useProxy
                ? _config.BuildProxyUrl()
                : _config.BuildGenerateContentUrl(apiKey);
            using var req = new UnityWebRequest(url, "POST")
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = _config.TimeoutSeconds,
            };
            req.SetRequestHeader("Content-Type", "application/json");

            var op = req.SendWebRequest();
            var tcs = new TaskCompletionSource<bool>();
            op.completed += _ => tcs.TrySetResult(true);

            using (ct.Register(() => { req.Abort(); tcs.TrySetCanceled(ct); }))
            {
                await tcs.Task.ConfigureAwait(true);
            }

            if (req.result != UnityWebRequest.Result.Success)
            {
                throw new GeminiCallException($"HTTP {req.responseCode}: {req.error} — {req.downloadHandler.text}");
            }
            return req.downloadHandler.text;
        }

        /// <summary>
        /// Wrap the system + user prompt into the generateContent body.
        /// Kept here (not in GeminiPromptBuilder) because the envelope is
        /// Gemini-specific transport; prompt-builder stays model-agnostic.
        /// </summary>
        public static string BuildRequestJson(string systemPrompt, string userPrompt)
        {
            var body = new GeminiRequestBody
            {
                systemInstruction = new ContentBlock { parts = new[] { new Part { text = systemPrompt } } },
                contents = new[]
                {
                    new ContentBlock { role = "user", parts = new[] { new Part { text = userPrompt } } }
                },
                generationConfig = new GenerationConfig
                {
                    temperature = 0.7f,
                    responseMimeType = "application/json",
                    // gemini-2.5-flash is a thinking model — by default
                    // it spends ~600+ tokens on internal reasoning per
                    // call, which pushed our action-generation latency
                    // past the 8s Unity / 10s proxy timeouts. Setting
                    // budget=0 turns off thinking entirely; the
                    // structured action JSON we ask for doesn't benefit
                    // from extended deliberation.
                    thinkingConfig = new ThinkingConfig { thinkingBudget = 0 },
                },
            };
            return JsonUtility.ToJson(body);
        }

        public static ChefActionResponse ParseResponse(string envelopeJson)
        {
            if (string.IsNullOrWhiteSpace(envelopeJson))
            {
                throw new GeminiCallException("Empty response body.");
            }

            GeminiResponseEnvelope envelope;
            try
            {
                envelope = JsonUtility.FromJson<GeminiResponseEnvelope>(envelopeJson);
            }
            catch (Exception ex)
            {
                throw new GeminiCallException("Failed to parse Gemini envelope.", ex);
            }

            var innerText = envelope?.candidates != null
                            && envelope.candidates.Length > 0
                            && envelope.candidates[0].content?.parts != null
                            && envelope.candidates[0].content.parts.Length > 0
                ? envelope.candidates[0].content.parts[0].text
                : null;

            if (string.IsNullOrWhiteSpace(innerText))
            {
                throw new GeminiCallException("Gemini envelope contained no text part.");
            }

            try
            {
                var parsed = JsonUtility.FromJson<ChefActionResponse>(innerText);
                if (parsed == null)
                {
                    throw new GeminiCallException("Inner JSON parsed to null.");
                }
                parsed.actions ??= Array.Empty<ChefAction>();
                parsed.monologue ??= string.Empty;
                return parsed;
            }
            catch (Exception ex)
            {
                throw new GeminiCallException(
                    $"Failed to parse inner ChefActionResponse JSON. Raw text:\n{innerText}", ex);
            }
        }

    }

    // --- Minimal Gemini REST wire types — only the fields we need ---
    // Hoisted out of GeminiClient so the Day 11 evaluator
    // (`GeminiRoundEvaluator`) can reuse the same envelope shapes
    // without a refactor when a third call lands.

    [Serializable]
    internal class GeminiRequestBody
    {
        public ContentBlock systemInstruction;
        public ContentBlock[] contents;
        public GenerationConfig generationConfig;
    }

    [Serializable]
    internal class ContentBlock
    {
        public string role;
        public Part[] parts;
    }

    [Serializable]
    internal class Part { public string text; }

    [Serializable]
    internal class GenerationConfig
    {
        public float temperature;
        public string responseMimeType;
        public ThinkingConfig thinkingConfig;
    }

    [Serializable]
    internal class ThinkingConfig
    {
        public int thinkingBudget;
    }

    [Serializable]
    internal class GeminiResponseEnvelope
    {
        public Candidate[] candidates;
    }

    [Serializable]
    internal class Candidate
    {
        public ContentBlock content;
        public string finishReason;
    }
}
