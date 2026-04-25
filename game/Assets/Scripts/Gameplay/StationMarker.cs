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
        [SerializeField] private Color _burstColor = new(1f, 1f, 1f, 1f);
        [SerializeField] private int _burstCount = 7;
        [SerializeField] private float _burstLifetime = 0.55f;
        [SerializeField] private Sprite _burstSprite;

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
            EmitBurst();
        }

        public void Configure(
            StationType type,
            string label,
            Color burstColor,
            Sprite burstSprite)
        {
            _stationType = type;
            _displayLabel = label ?? string.Empty;
            _burstColor = burstColor;
            _burstSprite = burstSprite;
        }

        private void EmitBurst()
        {
            if (_burstSprite == null || _burstCount <= 0) return;
            for (var i = 0; i < _burstCount; i++)
            {
                var go = new GameObject("Burst");
                // Don't parent — the station's localScale (2.2 × 1.6)
                // would distort the burst sprites and lose the directional
                // velocity; spawn in world space and clean up via Destroy.
                go.transform.position = transform.position;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _burstSprite;
                sr.color = _burstColor;
                sr.sortingOrder = 14;
                go.transform.localScale = Vector3.one * 0.18f;
                StartCoroutine(BurstRoutine(go, sr));
            }
        }

        private IEnumerator BurstRoutine(GameObject go, SpriteRenderer sr)
        {
            // Random initial direction, biased upward so the visual
            // reads as "energy rising off the station".
            var dir = new Vector3(
                Random.Range(-0.6f, 0.6f),
                Random.Range(0.4f, 1.2f),
                0f);
            var speed = Random.Range(1.4f, 2.6f);
            var t = 0f;
            var dur = _burstLifetime;
            var startColor = sr.color;
            while (t < dur)
            {
                t += Time.deltaTime;
                var k = t / dur;
                go.transform.position += dir * (speed * Time.deltaTime);
                go.transform.localScale = Vector3.one * Mathf.Lerp(0.18f, 0.04f, k);
                sr.color = new Color(
                    startColor.r,
                    startColor.g,
                    startColor.b,
                    Mathf.Lerp(startColor.a, 0f, k));
                yield return null;
            }
            if (go != null) Destroy(go);
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
