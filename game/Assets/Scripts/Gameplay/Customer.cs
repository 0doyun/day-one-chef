// Customer — the NPC at the counter who voiced the current Order.
// Day 4 wired the order bubble; Day 13 polish adds a face label that
// reacts to round outcome. ASCII faces only — NotoSansKR has no emoji
// glyphs and embedding a fallback emoji font is out of scope here.
//
// The speech bubble + face TMP_Texts are wired by MainKitchenSetup at
// scene authoring time. Customer.Configure / ReactToOutcome drive the
// labels as the round progresses.

using TMPro;
using UnityEngine;
using DayOneChef.Gameplay.Data;

namespace DayOneChef.Gameplay
{
    public class Customer : MonoBehaviour
    {
        // Face strings are static so each round's reset doesn't trigger a
        // GC alloc; they're also tweakable from one place if the look
        // needs adjusting (e.g. the sad face read as a wink in playtest).
        private const string FaceWaiting = ":|";
        private const string FaceHappy   = "^_^";
        private const string FaceSad     = ";_;";

        [SerializeField] private Order _currentOrder;
        [SerializeField] private CustomerMood _mood;
        [SerializeField] private TMP_Text _orderBubble;
        [SerializeField] private TMP_Text _faceLabel;
        [SerializeField] private string _emptyBubbleText = "주문 대기 중…";

        public Order CurrentOrder => _currentOrder;
        public CustomerMood Mood
        {
            get => _mood;
            set => _mood = value;
        }

        public void AttachBubble(TMP_Text bubble) => _orderBubble = bubble;
        public void AttachFace(TMP_Text face) => _faceLabel = face;

        public void Configure(Order order)
        {
            _currentOrder = order;
            _mood = order != null ? order.CustomerMood : CustomerMood.Waiting;
            RefreshBubble();
            SetFace(FaceWaiting);
        }

        public void ReactToOutcome(bool success)
        {
            SetFace(success ? FaceHappy : FaceSad);
        }

        private void Start()
        {
            RefreshBubble();
            SetFace(FaceWaiting);
        }

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
                ? $"{_currentOrder.Recipe.DisplayName} 주세요!"
                : _emptyBubbleText;
        }

        private void SetFace(string face)
        {
            if (_faceLabel == null) return;
            _faceLabel.text = face;
        }
    }
}
