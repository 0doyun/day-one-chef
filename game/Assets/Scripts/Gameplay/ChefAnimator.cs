// Visual polish for the Day 13 prototype kitchen. Drives the player
// sprite to the station each verb cares about, bobs on impact, and
// updates a small TMP label above the chef showing what's currently
// in hand. Pure presentation — KitchenState / EventLog stay
// authoritative, so flipping this component off does not change a
// single round outcome.
//
// Timing budget: each action animation must complete inside the
// executor's 1.0 s ACTION_TICK (Day 13 polish bump from the GDD §13
// 0.6s prototype — see ActionExecutor for the rationale). The
// defaults below give 0.55 s travel + 0.25 s bob = 0.80 s total,
// leaving 0.20 s of idle so the next OnAction call doesn't visibly
// chop a still-running coroutine.

using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using DayOneChef.Gameplay.Data;

namespace DayOneChef.Gameplay
{
    public class ChefAnimator : MonoBehaviour, IChefAnimator
    {
        [SerializeField] private float _moveDuration = 0.55f;
        [SerializeField] private float _bobDuration = 0.25f;
        [SerializeField] private float _bobHeight = 0.45f;
        [SerializeField] private Vector3 _stationApproachOffset = new(0f, -1.4f, 0f);
        [SerializeField] private TextMeshPro _heldLabel;

        private readonly Dictionary<StationType, StationMarker> _stations = new();
        private Vector3 _homePosition;
        private Coroutine _current;

        private void Awake()
        {
            _homePosition = transform.position;
            var markers = FindObjectsByType<StationMarker>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var m in markers)
            {
                _stations[m.StationType] = m;
            }
            ClearHeldLabel();
        }

        public void OnAction(ChefVerb verb, EventLogEntry entry, KitchenState kitchen)
        {
            if (_current != null)
            {
                StopCoroutine(_current);
                _current = null;
            }
            _current = StartCoroutine(PerformRoutine(verb, entry, kitchen));
        }

        private IEnumerator PerformRoutine(ChefVerb verb, EventLogEntry entry, KitchenState kitchen)
        {
            var station = ResolveStation(verb, entry, kitchen);
            var target = station != null
                ? station.transform.position + _stationApproachOffset
                : _homePosition;

            yield return MoveTo(target, _moveDuration);

            // Station feedback fires the moment the chef arrives. Skipped
            // actions get a softer flash since nothing actually happened.
            if (station != null && !entry.skipped) station.Flash();

            // Skipped actions still get a tiny "no-op" wiggle so a player
            // can see the executor moved on rather than froze.
            var amplitude = entry.skipped ? _bobHeight * 0.3f : _bobHeight;
            yield return Bob(amplitude, _bobDuration);

            UpdateHeldLabel(kitchen);
            _current = null;
        }

        private StationMarker ResolveStation(ChefVerb verb, EventLogEntry entry, KitchenState kitchen)
        {
            // Move is the only verb that names the station literally;
            // the other verbs map by what the action does.
            switch (verb)
            {
                case ChefVerb.Move:
                    return kitchen != null
                           && kitchen.TryResolveStation(entry.target, out var named)
                           && _stations.TryGetValue(named, out var sNamed)
                        ? sNamed
                        : null;
                case ChefVerb.Pickup:
                    return _stations.TryGetValue(StationType.Fridge, out var sFridge) ? sFridge : null;
                case ChefVerb.Chop:
                case ChefVerb.Crack:
                case ChefVerb.Mix:
                    return _stations.TryGetValue(StationType.CuttingBoard, out var sBoard) ? sBoard : null;
                case ChefVerb.Cook:
                    return _stations.TryGetValue(StationType.Stove, out var sStove) ? sStove : null;
                case ChefVerb.Assemble:
                    return _stations.TryGetValue(StationType.Assembly, out var sAssembly) ? sAssembly : null;
                case ChefVerb.Serve:
                    return _stations.TryGetValue(StationType.Counter, out var sCounter) ? sCounter : null;
                default:
                    return null;
            }
        }

        private IEnumerator MoveTo(Vector3 destination, float duration)
        {
            var start = transform.position;
            // Same-tile moves still need a >0 duration so the bob phase
            // doesn't fire on the same frame the executor started.
            var dur = Mathf.Max(duration, 0.05f);
            var t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                var k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
                transform.position = Vector3.Lerp(start, destination, k);
                yield return null;
            }
            transform.position = destination;
        }

        private IEnumerator Bob(float amplitude, float duration)
        {
            var start = transform.position;
            var dur = Mathf.Max(duration, 0.05f);
            var t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                // Sin wave 0 → π returns to start by the end so chained
                // bobs don't drift the chef upward over many actions.
                var k = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / dur));
                transform.position = start + new Vector3(0f, amplitude * k, 0f);
                yield return null;
            }
            transform.position = start;
        }

        private void UpdateHeldLabel(KitchenState kitchen)
        {
            if (_heldLabel == null) return;
            if (kitchen?.ChefHolding is not IngredientType type)
            {
                ClearHeldLabel();
                return;
            }
            var def = kitchen.GetDefinition(type);
            var name = def != null && !string.IsNullOrWhiteSpace(def.DisplayName)
                ? def.DisplayName
                : type.ToString();
            // NotoSansKR has no emoji glyphs, so the indicator stays Korean.
            _heldLabel.text = $"손에: {name}";
            _heldLabel.gameObject.SetActive(true);
        }

        private void ClearHeldLabel()
        {
            if (_heldLabel == null) return;
            _heldLabel.text = string.Empty;
            _heldLabel.gameObject.SetActive(false);
        }

        public void ResetVisuals()
        {
            if (_current != null)
            {
                StopCoroutine(_current);
                _current = null;
            }
            transform.position = _homePosition;
            ClearHeldLabel();
        }

        // Editor-only helper so MainKitchenSetup can wire the held-label
        // child without going through SerializedObject for one ref.
        public void SetHeldLabel(TextMeshPro label) => _heldLabel = label;
    }
}
