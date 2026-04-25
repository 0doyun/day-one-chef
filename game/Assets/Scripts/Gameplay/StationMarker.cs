// Station marker for the Day 3 prototype kitchen.
// Attached to each of the 5 stations (냉장고 / 도마 / 화구 / 조립대 / 카운터).
// Day 13 added Flash() — a brief scale-up + color brighten the chef
// animator triggers when the player arrives at the station, so the
// eye gets feedback that "this is the station the verb resolved to".

using System.Collections;
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
        [SerializeField] private float _flashDuration = 0.45f;
        [SerializeField] private float _flashScale = 1.18f;
        [SerializeField] private float _flashBrightness = 0.4f;

        private SpriteRenderer _sr;
        private Vector3 _baseScale;
        private Color _baseColor;
        private Coroutine _activeFlash;

        public StationType StationType => _stationType;
        public string DisplayLabel => _displayLabel;

        public void Configure(StationType type, string label)
        {
            _stationType = type;
            _displayLabel = label ?? string.Empty;
        }

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _baseScale = transform.localScale;
            if (_sr != null) _baseColor = _sr.color;
        }

        public void Flash()
        {
            if (!isActiveAndEnabled) return;
            if (_activeFlash != null) StopCoroutine(_activeFlash);
            _activeFlash = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            var dur = Mathf.Max(_flashDuration, 0.05f);
            var t = 0f;
            // First half = ramp up, second half = ramp back.
            while (t < dur)
            {
                t += Time.deltaTime;
                var phase = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / dur));
                var s = Mathf.Lerp(1f, _flashScale, phase);
                transform.localScale = _baseScale * s;
                if (_sr != null)
                {
                    var lift = _flashBrightness * phase;
                    _sr.color = new Color(
                        Mathf.Clamp01(_baseColor.r + lift),
                        Mathf.Clamp01(_baseColor.g + lift),
                        Mathf.Clamp01(_baseColor.b + lift),
                        _baseColor.a);
                }
                yield return null;
            }
            transform.localScale = _baseScale;
            if (_sr != null) _sr.color = _baseColor;
            _activeFlash = null;
        }

        private void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }
    }
}
