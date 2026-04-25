// Result of Gemini call #2 — the evaluator. Drives the success /
// reason fields on the bridge's `round_end` payload (ADR-0002).
//
// `success` is the binary verdict: did the chef produce the
// customer's order. `reason` is a single-sentence Korean summary of
// why — surfaced in the future Day 13 result log so the player sees
// "패티가 익지 않았어요" rather than a silent failure.
//
// Marked [Serializable] because the response from Gemini is parsed
// straight into this struct via JsonUtility — keep field names in
// sync with EvaluatorPromptBuilder's response-format instructions.

using System;

namespace DayOneChef.Gameplay.AI
{
    [Serializable]
    public class RoundEvaluation
    {
        public bool success;
        public string reason;

        public static RoundEvaluation Failure(string reason) => new() { success = false, reason = reason };
    }
}
