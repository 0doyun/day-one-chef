// Round controller. Day 5 adds SubmitInstructionAsync — the call that
// ships player 지시 to Gemini and logs the returned actions/monologue.
// Execution of the action array is Day 6 (action executor + event_log);
// evaluation is Day 11. Until those land, SubmitInstructionAsync logs
// the response and auto-advances the round so the whole queue can be
// exercised end-to-end in the Editor.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using DayOneChef.Bridge;
using DayOneChef.Gameplay.AI;
using DayOneChef.Gameplay.Data;

namespace DayOneChef.Gameplay
{
    public enum RoundPhase
    {
        Idle,
        AwaitingInstruction,
        CallingGemini,
        // Executing / Evaluating land in Day 6 / Day 11.
        Done,
    }

    public class GameRound : MonoBehaviour
    {
        [SerializeField] private OrderCatalog _catalog;
        [SerializeField] private Customer _customer;
        [SerializeField] private GeminiConfig _geminiConfig;
        [SerializeField] private IngredientDefinition[] _ingredientDefinitions;
        [SerializeField] private List<string> _availableIngredientsForPrompt = new()
        {
            "패티", "빵", "치즈", "상추", "토마토", "계란",
        };
        [SerializeField] private List<string> _availableStationsForPrompt = new()
        {
            "냉장고", "도마", "화구", "조립대", "카운터",
        };

        private OrderQueue _queue;
        private RoundPhase _phase = RoundPhase.Idle;
        private IGeminiClient _client;
        private IRoundEvaluator _evaluator;
        private KitchenState _kitchen;
        private ActionExecutor _executor;
        private EventLog _lastEventLog;
        private RoundEvaluation _lastEvaluation;

        public RoundPhase Phase => _phase;
        public Order CurrentOrder => _queue?.Current;
        public int RoundIndex => _queue?.ProcessedCount ?? 0;
        public int TotalRounds => _queue?.Count ?? 0;
        public Customer Customer => _customer;
        public EventLog LastEventLog => _lastEventLog;
        public KitchenState Kitchen => _kitchen;

        public void Bind(OrderCatalog catalog, Customer customer, GeminiConfig geminiConfig = null)
        {
            _catalog = catalog;
            _customer = customer;
            if (geminiConfig != null) _geminiConfig = geminiConfig;
        }

        public void BindIngredients(IngredientDefinition[] defs)
        {
            _ingredientDefinitions = defs;
        }

        /// <summary>
        /// Inject a custom client (e.g. a Flutter-proxy implementation, or
        /// a test double). If not called, a direct <see cref="GeminiClient"/>
        /// is constructed lazily from <see cref="_geminiConfig"/>.
        /// </summary>
        public void SetClient(IGeminiClient client) => _client = client;

        private int _successCount;
        private int _failCount;

        private void Start()
        {
            if (_catalog == null)
            {
                Debug.LogError("[GameRound] OrderCatalog is missing — round cannot start.");
                return;
            }
            _queue = new OrderQueue(_catalog.Orders);
            RebuildKitchen();
            PresentCurrentOrder();
        }

        private void OnEnable()
        {
            BridgeIncoming.OnResetRoundRequested += HandleResetRoundFromBridge;
            BridgeIncoming.OnSessionRestartRequested += HandleSessionRestartFromBridge;
        }

        private void OnDisable()
        {
            BridgeIncoming.OnResetRoundRequested -= HandleResetRoundFromBridge;
            BridgeIncoming.OnSessionRestartRequested -= HandleSessionRestartFromBridge;
        }

        private void HandleResetRoundFromBridge()
        {
            RebuildKitchen();
            PresentCurrentOrder();
        }

        private void HandleSessionRestartFromBridge()
        {
            _queue = new OrderQueue(_catalog.Orders);
            _successCount = 0;
            _failCount = 0;
            RebuildKitchen();
            PresentCurrentOrder();
        }

        private void RebuildKitchen()
        {
            var stations = FindObjectsByType<StationMarker>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            _kitchen = new KitchenState(_ingredientDefinitions ?? System.Array.Empty<IngredientDefinition>(), stations);
            _executor = new ActionExecutor(_kitchen);
        }

        public async Task SubmitInstructionAsync(string instruction, CancellationToken ct = default)
        {
            if (_phase != RoundPhase.AwaitingInstruction)
            {
                Debug.LogWarning($"[GameRound] Ignoring SubmitInstructionAsync while phase={_phase}.");
                return;
            }
            if (string.IsNullOrWhiteSpace(instruction))
            {
                Debug.LogWarning("[GameRound] Empty instruction ignored (GDD §14 edge case — UI should gate this).");
                return;
            }

            _phase = RoundPhase.CallingGemini;
            var order = _queue.Current;
            var snapshot = new GameStateSnapshot(
                order?.Recipe?.DisplayName ?? string.Empty,
                order?.ExampleInstruction ?? string.Empty,
                _availableIngredientsForPrompt,
                _availableStationsForPrompt);

            string failureReason = null;
            _lastEvaluation = null;
            try
            {
                var client = _client ?? CreateDefaultClient();
                var response = await client.GenerateActionsAsync(snapshot, instruction, ct);
                LogResponse(order, response);
                if (_executor != null)
                {
                    _lastEventLog = await _executor.ExecuteAsync(response, ct);
                    Debug.Log($"[GameRound] EventLog JSON: {_lastEventLog.ToJson()}");
                }
                _lastEvaluation = await EvaluateRoundAsync(order, instruction, ct);
                Debug.Log($"[GameRound] Evaluator: success={_lastEvaluation.success} reason=\"{_lastEvaluation.reason}\"");
            }
            catch (GeminiCallException ex)
            {
                failureReason = ex.Message;
                Debug.LogError($"[GameRound] Gemini call failed — round invalidated per GDD §4.3: {ex.Message}");
            }
            finally
            {
                EmitRoundEnd(order, instruction, failureReason);
                AdvanceRound();
            }
        }

        private async Task<RoundEvaluation> EvaluateRoundAsync(Order order, string instruction, CancellationToken ct)
        {
            var evaluator = _evaluator ?? CreateDefaultEvaluator();
            if (evaluator == null || _lastEventLog == null || _kitchen == null)
            {
                return RoundEvaluation.Failure("evaluator not configured");
            }

            var ctx = new EvaluationContext
            {
                Order = order,
                PlayerInstruction = instruction,
                EventLog = _lastEventLog,
                FinalState = _kitchen.State,
            };
            try
            {
                return await evaluator.EvaluateAsync(ctx, ct);
            }
            catch (GeminiCallException ex)
            {
                Debug.LogWarning($"[GameRound] Evaluator failed, treating round as fail: {ex.Message}");
                return RoundEvaluation.Failure($"evaluator error: {ex.Message}");
            }
        }

        private IRoundEvaluator CreateDefaultEvaluator()
        {
            if (_geminiConfig == null) return null;
            _evaluator = new GeminiRoundEvaluator(_geminiConfig);
            return _evaluator;
        }

        private void EmitRoundEnd(Order order, string instruction, string failureReason)
        {
            // Day 11: Gemini call #2 (the evaluator) fills success +
            // reason. If the evaluator never ran (call #1 threw before
            // executor finished), `failureReason` carries the upstream
            // error; otherwise `_lastEvaluation` is authoritative.
            bool success;
            string reason;
            if (failureReason != null)
            {
                success = false;
                reason = failureReason;
            }
            else if (_lastEvaluation != null)
            {
                success = _lastEvaluation.success;
                reason = _lastEvaluation.reason;
            }
            else
            {
                success = false;
                reason = "evaluator not run";
            }
            if (success) _successCount++; else _failCount++;

            var eventLogJson = _lastEventLog?.ToJson() ?? "{\"entries\":[]}";
            UnityBridge.Send(BridgeMessage.RoundEnd(
                orderId: order?.OrderId ?? string.Empty,
                orderTitle: order?.Recipe?.DisplayName ?? string.Empty,
                roundIndex: RoundIndex,
                totalRounds: TotalRounds,
                instruction: instruction ?? string.Empty,
                success: success,
                reason: reason,
                eventLogJson: eventLogJson));

            var isFinal = (RoundIndex + 1) >= TotalRounds;
            if (isFinal)
            {
                UnityBridge.Send(BridgeMessage.SessionEnd(_successCount, _failCount, TotalRounds));
            }
        }

        public void AdvanceRound()
        {
            if (_queue == null) return;
            _queue.Advance();
            RebuildKitchen();
            PresentCurrentOrder();
        }

        private IGeminiClient CreateDefaultClient()
        {
            if (_geminiConfig == null)
            {
                throw new GeminiCallException(
                    "GameRound.GeminiConfig not assigned. MainKitchenSetup wires this — " +
                    "if you're instantiating GameRound outside Setup, assign a config or call SetClient().");
            }
            return new GeminiClient(_geminiConfig);
        }

        private static void LogResponse(Order order, ChefActionResponse response)
        {
            Debug.Log(
                $"[GameRound] Gemini response for {order?.OrderId}: " +
                $"actions={response.actions?.Length ?? 0}, monologue=\"{response.monologue}\"");
            if (response.actions != null)
            {
                for (var i = 0; i < response.actions.Length; i++)
                {
                    var a = response.actions[i];
                    Debug.Log($"  [{i}] verb={a.verb} target={a.target} param={a.param}");
                }
            }
        }

        private void PresentCurrentOrder()
        {
            if (_queue == null) return;
            if (_queue.IsExhausted)
            {
                _phase = RoundPhase.Done;
                if (_customer != null) _customer.Configure(null);
                Debug.Log("[GameRound] All orders processed — round loop complete.");
                return;
            }
            _phase = RoundPhase.AwaitingInstruction;
            if (_customer != null) _customer.Configure(_queue.Current);
            Debug.Log(
                $"[GameRound] Round {_queue.ProcessedCount + 1}/{_queue.Count}: " +
                $"{_queue.Current?.OrderId} — {_queue.Current?.Recipe?.DisplayName}");
        }
    }
}
