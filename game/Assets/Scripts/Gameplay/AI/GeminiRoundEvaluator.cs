// Gemini call #2 — the evaluator. Reuses the same Gemini envelope
// (GeminiRequestBody / GeminiResponseEnvelope) and HTTP transport as
// the action generator (call #1), differing only in:
//   - Lower temperature (we want a stable verdict, not creative prose)
//   - Different system + user prompt (EvaluatorPromptBuilder)
//   - Different inner schema (RoundEvaluation rather than ChefActionResponse)
//
// Same proxy / direct switch as GeminiClient (#if UNITY_WEBGL).
// Editor Play talks to Google directly with the EditorPrefs key;
// shipped WebGL goes through the Flutter shelf proxy.

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using DayOneChef.Gameplay.Data;

namespace DayOneChef.Gameplay.AI
{
    public class GeminiRoundEvaluator : IRoundEvaluator
    {
        // Day 11 evaluator wants a stable verdict, not a creative prose
        // riff — keep the temperature low so identical inputs converge
        // on identical judgements.
        private const float EvaluatorTemperature = 0.2f;

        private readonly GeminiConfig _config;
        private readonly Func<string> _apiKeyProvider;

        public GeminiRoundEvaluator(GeminiConfig config, Func<string> apiKeyProvider = null)
        {
            _config = config != null ? config : throw new ArgumentNullException(nameof(config));
            _apiKeyProvider = apiKeyProvider ?? GeminiCredentials.GetApiKey;
        }

        public async Task<RoundEvaluation> EvaluateAsync(EvaluationContext context, CancellationToken ct = default)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            const bool useProxy = true;
            string apiKey = null;
#else
            const bool useProxy = false;
            var apiKey = _apiKeyProvider();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new GeminiCallException(
                    "No API key configured for evaluator. Set the Gemini key in the editor.");
            }
#endif

            var systemPrompt = EvaluatorPromptBuilder.BuildSystemPrompt();
            var userPrompt = EvaluatorPromptBuilder.BuildUserPrompt(context);
            var requestBody = BuildEvaluatorRequestJson(systemPrompt, userPrompt);

            Exception lastError = null;
            for (var attempt = 0; attempt <= _config.Retries; attempt++)
            {
                try
                {
                    var responseText = await SendAsync(apiKey, requestBody, ct, useProxy).ConfigureAwait(true);
                    return ParseEvaluation(responseText);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    lastError = ex;
                    Debug.LogWarning($"[GeminiRoundEvaluator] Attempt {attempt + 1} failed: {ex.Message}");
                }
            }
            throw new GeminiCallException(
                $"Evaluator call failed after {_config.Retries + 1} attempt(s).", lastError);
        }

        private async Task<string> SendAsync(string apiKey, string body, CancellationToken ct, bool useProxy)
        {
            var url = useProxy ? _config.BuildProxyUrl() : _config.BuildGenerateContentUrl(apiKey);
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

        public static string BuildEvaluatorRequestJson(string systemPrompt, string userPrompt)
        {
            // Same envelope as GeminiClient.BuildRequestJson — different
            // temperature and prompt-builder. Inlined here so the
            // evaluator stays self-contained for tests; if a third call
            // shows up we extract a shared GeminiEnvelope helper.
            var body = new GeminiRequestBody
            {
                systemInstruction = new ContentBlock { parts = new[] { new Part { text = systemPrompt } } },
                contents = new[]
                {
                    new ContentBlock { role = "user", parts = new[] { new Part { text = userPrompt } } }
                },
                generationConfig = new GenerationConfig
                {
                    temperature = EvaluatorTemperature,
                    responseMimeType = "application/json",
                },
            };
            return JsonUtility.ToJson(body);
        }

        public static RoundEvaluation ParseEvaluation(string envelopeJson)
        {
            if (string.IsNullOrWhiteSpace(envelopeJson))
            {
                throw new GeminiCallException("Empty evaluator response body.");
            }

            GeminiResponseEnvelope envelope;
            try
            {
                envelope = JsonUtility.FromJson<GeminiResponseEnvelope>(envelopeJson);
            }
            catch (Exception ex)
            {
                throw new GeminiCallException("Failed to parse evaluator envelope.", ex);
            }

            var innerText = envelope?.candidates != null
                            && envelope.candidates.Length > 0
                            && envelope.candidates[0].content?.parts != null
                            && envelope.candidates[0].content.parts.Length > 0
                ? envelope.candidates[0].content.parts[0].text
                : null;

            if (string.IsNullOrWhiteSpace(innerText))
            {
                throw new GeminiCallException("Evaluator envelope contained no text part.");
            }

            try
            {
                var result = JsonUtility.FromJson<RoundEvaluation>(innerText);
                if (result == null)
                {
                    throw new GeminiCallException("Evaluator JSON parsed to null.");
                }
                if (string.IsNullOrEmpty(result.reason))
                {
                    result.reason = result.success ? "성공" : "사유 미명시";
                }
                return result;
            }
            catch (Exception ex)
            {
                throw new GeminiCallException(
                    $"Failed to parse evaluator inner JSON: {innerText}", ex);
            }
        }
    }
}
