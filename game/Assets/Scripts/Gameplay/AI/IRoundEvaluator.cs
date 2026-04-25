// Day 11 evaluator (Gemini call #2) — judges whether a played round
// satisfied the order. Decoupled behind an interface so editor /
// offline tests can swap in a deterministic stub. See ADR-0005.

using System.Threading;
using System.Threading.Tasks;

namespace DayOneChef.Gameplay.AI
{
    public interface IRoundEvaluator
    {
        Task<RoundEvaluation> EvaluateAsync(EvaluationContext context, CancellationToken ct = default);
    }
}
