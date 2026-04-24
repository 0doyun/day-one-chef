// Customer — the NPC at the counter who voiced the current Order.
// For Day 4 the only gameplay-visible attribute is which Order they
// are holding; mood is presentation-only. Day 11/12 may introduce
// mood-based feedback once evaluation is in place.
//
// The speech bubble TMP_Text is wired in by MainKitchenSetup at scene
// authoring time via AttachBubble. Customer.Configure then drives the
// bubble text as the round progresses.

using TMPro;
using UnityEngine;
using DayOneChef.Gameplay.Data;

namespace DayOneChef.Gameplay
{
    public class Customer : MonoBehaviour
    {
        [SerializeField] private Order _currentOrder;
        [SerializeField] private CustomerMood _mood;
        [SerializeField] private TMP_Text _orderBubble;
        [SerializeField] private string _emptyBubbleText = "주문 대기 중…";

        public Order CurrentOrder => _currentOrder;
        public CustomerMood Mood
        {
            get => _mood;
            set => _mood = value;
        }

        public void AttachBubble(TMP_Text bubble) => _orderBubble = bubble;

        public void Configure(Order order)
        {
            _currentOrder = order;
            _mood = order != null ? order.CustomerMood : CustomerMood.Waiting;
            RefreshBubble();
        }

        private void Start() => RefreshBubble();

        private void RefreshBubble()
        {
            if (_orderBubble == null)
            {
                Debug.LogWarning(
                    $"[Customer] {name} has no _orderBubble reference — " +
                    "MainKitchenSetup.AttachBubble did not serialize. The order " +
                    "bubble will keep its authored-time placeholder.");
                return;
            }
            _orderBubble.text = _currentOrder != null && _currentOrder.Recipe != null
                ? _currentOrder.Recipe.DisplayName
                : _emptyBubbleText;
        }
    }
}
