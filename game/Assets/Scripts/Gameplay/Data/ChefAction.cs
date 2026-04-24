// Data carriers for Gemini call #1's structured JSON response —
// see design/gdd/game-concept.md §4.1. `Verb` arrives as a lowercase
// string from Gemini and is parsed into the ChefVerb enum via
// ChefActionParser; keeping Verb as a string lets the client log the
// original token when enum parsing fails (a common Gemini edge case).

using System;

namespace DayOneChef.Gameplay.Data
{
    [Serializable]
    public class ChefAction
    {
        public string verb;
        public string target;
        public string param;

        // Set to true by the action executor (Day 6) when the verb is
        // unknown or the target doesn't resolve. Kept on the data class
        // itself so the downstream event_log can be serialised back to
        // Gemini call #2 (evaluator) without extra bookkeeping.
        public bool skipped;
    }

    [Serializable]
    public class ChefActionResponse
    {
        public ChefAction[] actions;
        public string monologue;
    }
}
