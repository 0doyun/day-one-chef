// One timestamped record of a chef action executed during a round.
// The event log is the ground truth Gemini call #2 reads on Day 11 to
// evaluate success/failure — particularly for order-sensitive recipes
// like 계란찜 where "crack before mix water" determines pass/fail.
//
// [Serializable] so JsonUtility can produce the exact wire shape the
// evaluator prompt consumes.

using System;

namespace DayOneChef.Gameplay.Data
{
    [Serializable]
    public class EventLogEntry
    {
        public string verb;
        public string target;
        public string param;
        public float t;             // seconds since round start
        public bool skipped;        // GDD §4.3 — unknown verb / missing target
        public string reason;       // why it was skipped (nullable)
        public string resolvedType; // IngredientType / StationType resolved (debug)
        public string resolvedState;// IngredientState after the verb ran (debug / eval)
    }
}
