// Station marker for the Day 3 prototype kitchen.
// Attached to each of the 5 stations (냉장고 / 도마 / 화구 / 조립대 / 카운터).
// Later phases will consume StationType to route chef actions and emit
// ground-truth events for Gemini evaluation.

using UnityEngine;

namespace DayOneChef.Gameplay
{
    public enum StationType
    {
        Fridge,       // 냉장고
        CuttingBoard, // 도마
        Stove,        // 화구
        Assembly,     // 조립대
        Counter,      // 카운터
    }

    [RequireComponent(typeof(Collider2D))]
    public class StationMarker : MonoBehaviour
    {
        [SerializeField] private StationType _stationType;
        [SerializeField] private string _displayLabel;

        public StationType StationType => _stationType;
        public string DisplayLabel => _displayLabel;

        public void Configure(StationType type, string label)
        {
            _stationType = type;
            _displayLabel = label ?? string.Empty;
        }

        private void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }
    }
}
