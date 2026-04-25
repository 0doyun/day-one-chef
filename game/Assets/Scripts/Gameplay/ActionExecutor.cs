// Executes Gemini's actions[] against KitchenState at a fixed tick,
// building an EventLog along the way. See ADR-0004 for why this is a
// state-machine-with-per-verb-handlers rather than a monolithic
// interpreter or full Command pattern.
//
// The executor is intentionally dumb about animation: every verb
// resolves, mutates state, appends an event, waits ACTION_TICK
// (GDD §13, 0.6s), loops. Day 7-13 polish hangs player movement +
// station VFX off the per-verb handlers.

using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using DayOneChef.Gameplay.AI;
using DayOneChef.Gameplay.Data;

namespace DayOneChef.Gameplay
{
    public class ActionExecutor
    {
        // GDD §13 set ACTION_TICK to 0.6s for the headless prototype.
        // Day 13-B bumped this to 1.5s — at 1.0s the chef finished
        // the recipe before the player could read the monologue
        // bubble. Round budget extends to ~5 verbs × 1.5s = 7.5s
        // execution + ~2s evaluator = ~9.5s, still inside the
        // ADR-0005 ≤10s window for the comedy beat to land.
        private const float ActionTickSeconds = 1.5f;

        private readonly KitchenState _kitchen;
        private readonly IChefAnimator _animator;

        public ActionExecutor(KitchenState kitchen) : this(kitchen, null) { }

        public ActionExecutor(KitchenState kitchen, IChefAnimator animator)
        {
            _kitchen = kitchen ?? throw new System.ArgumentNullException(nameof(kitchen));
            _animator = animator;
        }

        /// <summary>
        /// Iterate the response's actions at ACTION_TICK cadence, pumping
        /// each through the verb dispatch table and recording an event
        /// log entry per step.
        /// </summary>
        public async Task<EventLog> ExecuteAsync(ChefActionResponse response, CancellationToken ct = default)
        {
            var log = new EventLog();
            if (response?.actions == null || response.actions.Length == 0)
            {
                Debug.Log("[ActionExecutor] Empty actions array — chef stands confused. (GDD §14)");
                return log;
            }

            var startTime = Time.time;
            for (var i = 0; i < response.actions.Length; i++)
            {
                ct.ThrowIfCancellationRequested();
                var action = response.actions[i];
                var entry = ApplyAction(action, Time.time - startTime);
                log.Append(entry);
                Debug.Log(
                    $"[ActionExecutor] t={entry.t:F2}s #{i} verb={entry.verb} target={entry.target} " +
                    $"param={entry.param} skipped={entry.skipped} reason={entry.reason}");
                NotifyAnimator(action, entry);

                if (i < response.actions.Length - 1)
                {
                    // `Task.Delay` does not resume on Unity's main thread
                    // in WebGL builds — the .NET threadpool isn't there
                    // to fire the timer continuation, so the executor
                    // would stall after the first action. Unity 6's
                    // `Awaitable.WaitForSecondsAsync` is coroutine-backed
                    // and survives the single-threaded WebGL runtime.
                    await Awaitable.WaitForSecondsAsync(ActionTickSeconds);
                    ct.ThrowIfCancellationRequested();
                }
            }
            Debug.Log($"[ActionExecutor] Done. entries={log.Count} finalState=({_kitchen.DumpFinalState()})");
            return log;
        }

        private EventLogEntry ApplyAction(ChefAction action, float t)
        {
            var entry = new EventLogEntry
            {
                verb = action?.verb,
                target = action?.target,
                param = action?.param,
                t = t,
            };
            if (action == null)
            {
                entry.skipped = true;
                entry.reason = "null action";
                return entry;
            }

            if (!GeminiPromptBuilder.TryParseVerb(action.verb, out var verb))
            {
                entry.skipped = true;
                entry.reason = "unknown verb";
                return entry;
            }

            switch (verb)
            {
                case ChefVerb.Pickup:   ApplyPickup(entry); break;
                case ChefVerb.Cook:     ApplyCook(entry); break;
                case ChefVerb.Chop:     ApplyChop(entry); break;
                case ChefVerb.Crack:    ApplyCrack(entry); break;
                case ChefVerb.Mix:      ApplyMix(entry); break;
                case ChefVerb.Assemble: ApplyAssemble(entry); break;
                case ChefVerb.Serve:    ApplyServe(entry); break;
                case ChefVerb.Move:     ApplyMove(entry); break;
                default:
                    entry.skipped = true;
                    entry.reason = $"unhandled verb {verb}";
                    break;
            }
            return entry;
        }

        // --- Per-verb handlers ---

        private void ApplyPickup(EventLogEntry e)
        {
            if (!_kitchen.TryResolveIngredient(e.target, out var type))
            {
                Skip(e, $"unknown ingredient '{e.target}'");
                return;
            }
            _kitchen.SetHolding(type);
            e.resolvedType = type.ToString();
            e.resolvedState = _kitchen.GetState(type).ToString();
        }

        private void ApplyCook(EventLogEntry e)
        {
            if (!_kitchen.TryResolveIngredient(e.target, out var type))
            {
                Skip(e, $"unknown ingredient '{e.target}'");
                return;
            }
            // For Day 6 we map all cook params to the Cooked terminal state;
            // a burn path (over-cook → Burnt) can be added when we have UI
            // signalling player intent for timing.
            if (!_kitchen.TrySetState(type, IngredientState.Cooked, out var reason))
            {
                Skip(e, reason);
                return;
            }
            e.resolvedType = type.ToString();
            e.resolvedState = IngredientState.Cooked.ToString();
        }

        private void ApplyChop(EventLogEntry e)
        {
            if (!_kitchen.TryResolveIngredient(e.target, out var type))
            {
                Skip(e, $"unknown ingredient '{e.target}'");
                return;
            }
            if (!_kitchen.TrySetState(type, IngredientState.Chopped, out var reason))
            {
                Skip(e, reason);
                return;
            }
            e.resolvedType = type.ToString();
            e.resolvedState = IngredientState.Chopped.ToString();
        }

        private void ApplyCrack(EventLogEntry e)
        {
            if (!_kitchen.TryResolveIngredient(e.target, out var type))
            {
                Skip(e, $"unknown ingredient '{e.target}'");
                return;
            }
            if (!_kitchen.TrySetState(type, IngredientState.Cracked, out var reason))
            {
                Skip(e, reason);
                return;
            }
            e.resolvedType = type.ToString();
            e.resolvedState = IngredientState.Cracked.ToString();
        }

        private void ApplyMix(EventLogEntry e)
        {
            if (!_kitchen.TryResolveIngredient(e.target, out var type))
            {
                Skip(e, $"unknown ingredient '{e.target}'");
                return;
            }
            // 2-step rule per GDD §2 #5: mix on a Cracked egg → Mixed
            // (water+salt folded in); mix on a Beaten egg → Beaten
            // (no additional effect). Rejecting any other state keeps
            // the order-sensitivity test in 계란찜 meaningful.
            var current = _kitchen.GetState(type);
            var next = current == IngredientState.Cracked
                ? IngredientState.Mixed
                : IngredientState.Beaten;

            if (!_kitchen.TrySetState(type, next, out var reason))
            {
                Skip(e, reason);
                return;
            }
            e.resolvedType = type.ToString();
            e.resolvedState = next.ToString();
        }

        private void ApplyAssemble(EventLogEntry e)
        {
            // Assembly is presentation-only for Day 6; the kitchen state
            // already tracks every required ingredient, and Day 11's
            // evaluator reads the final state map.
            e.resolvedType = e.target;
        }

        private void ApplyServe(EventLogEntry e)
        {
            // Serve is a round terminator — the evaluator sees this as
            // "chef considers the dish done". No state mutation.
            e.resolvedType = e.target;
        }

        private void ApplyMove(EventLogEntry e)
        {
            if (!_kitchen.TryResolveStation(e.target, out var station))
            {
                Skip(e, $"unknown station '{e.target}'");
                return;
            }
            e.resolvedType = station.ToString();
        }

        private static void Skip(EventLogEntry e, string reason)
        {
            e.skipped = true;
            e.reason = reason;
        }

        private void NotifyAnimator(ChefAction action, EventLogEntry entry)
        {
            if (_animator == null) return;
            // Skipped entries with unknown verbs are still surfaced so the
            // animator can render a no-op wiggle; only swallow when the
            // verb itself is unparseable, since then we have no enum to
            // give the animator.
            if (!GeminiPromptBuilder.TryParseVerb(action?.verb, out var verb)) return;
            _animator.OnAction(verb, entry, _kitchen);
        }
    }
}
