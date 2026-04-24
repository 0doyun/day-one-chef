// Round controller. For Day 4 the state machine is still a stub —
// Gemini action + evaluation land on Day 5/6/11. Currently it just
// moves through: Idle → AwaitingInstruction → Done (on manual Advance).
// The key Day 4 deliverable is: wire an OrderCatalog into a queue,
// spawn / reconfigure the Customer with the current order, and expose
// the current round status for future consumers (Gemini client, HUD).

using UnityEngine;
using DayOneChef.Gameplay.Data;

namespace DayOneChef.Gameplay
{
    public enum RoundPhase
    {
        Idle,
        AwaitingInstruction,
        // Executing / Evaluating land in Day 5–11.
        Done,
    }

    public class GameRound : MonoBehaviour
    {
        [SerializeField] private OrderCatalog _catalog;
        [SerializeField] private Customer _customer;

        private OrderQueue _queue;
        private RoundPhase _phase = RoundPhase.Idle;

        public RoundPhase Phase => _phase;
        public Order CurrentOrder => _queue?.Current;
        public int RoundIndex => _queue?.ProcessedCount ?? 0;
        public int TotalRounds => _queue?.Count ?? 0;

        public Customer Customer => _customer;

        public void Bind(OrderCatalog catalog, Customer customer)
        {
            _catalog = catalog;
            _customer = customer;
        }

        private void Start()
        {
            if (_catalog == null)
            {
                Debug.LogError("[GameRound] OrderCatalog is missing — round cannot start.");
                return;
            }
            _queue = new OrderQueue(_catalog.Orders);
            PresentCurrentOrder();
        }

        public void AdvanceRound()
        {
            if (_queue == null) return;
            _queue.Advance();
            PresentCurrentOrder();
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
