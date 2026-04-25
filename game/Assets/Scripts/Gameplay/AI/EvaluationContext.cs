// Bundles every piece of ground truth the Day 11 evaluator (Gemini
// call #2) needs to render a verdict. Constructed at the end of a
// round in GameRound.SubmitInstructionAsync.
//
// Intentionally a plain class rather than a Serializable — the
// EvaluatorPromptBuilder hand-renders this into Korean prose, so we
// don't want JsonUtility shape constraints leaking into how the
// fields are typed.

using System.Collections.Generic;
using DayOneChef.Gameplay.Data;

namespace DayOneChef.Gameplay.AI
{
    public class EvaluationContext
    {
        public Order Order;
        public string PlayerInstruction;
        public EventLog EventLog;
        public IReadOnlyDictionary<IngredientType, IngredientState> FinalState;
    }
}
