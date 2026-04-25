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
        [SerializeField] private SpriteRenderer _heldItemSr;
        [SerializeField] private IngredientSpriteEntry[] _ingredientSprites;
        // Day 13-B: little "…" thought bubble above the chef while the
        // Gemini call is in flight. Replaces the bottom-of-screen status
        // text "셰프가 머리를 굴리는 중…" — players read the chef's head,
        // not text rows under the order card.
        [SerializeField] private GameObject _thinkingRoot;
        [SerializeField] private TextMeshPro _thinkingText;

        [System.Serializable]
        public struct IngredientSpriteEntry
        {
            public IngredientType type;
            public Sprite sprite;
        }

        private readonly Dictionary<StationType, StationMarker> _stations = new();
        private Vector3 _homePosition;
        private Coroutine _current;
        // Visual-only held tracker. KitchenState.ChefHolding only changes
        // on Pickup, but the chef is also visibly handling ingredients
        // during Chop/Cook/Crack/Mix — Gemini frequently emits those verbs
        // without an explicit prior Pickup, so reading ChefHolding alone
        // would freeze the held sprite on whatever was last picked up.
        private IngredientType? _displayedHeld;
        private Coroutine _thinkingLoop;

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

            UpdateDisplayedHeld(verb, entry, kitchen);
            UpdateHeldLabel(kitchen);
            _current = null;
        }

        private void UpdateDisplayedHeld(ChefVerb verb, EventLogEntry entry, KitchenState kitchen)
        {
            if (entry.skipped) return;
            switch (verb)
            {
                case ChefVerb.Pickup:
                    _displayedHeld = kitchen?.ChefHolding;
                    return;
                case ChefVerb.Chop:
                case ChefVerb.Cook:
                case ChefVerb.Crack:
                case ChefVerb.Mix:
                    if (kitchen != null && kitchen.TryResolveIngredient(entry.target, out var t))
                        _displayedHeld = t;
                    return;
                case ChefVerb.Serve:
                    _displayedHeld = null;
                    return;
                // Move and Assemble keep the current held visual.
            }
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
            if (_displayedHeld is not IngredientType type)
            {
                ClearHeldLabel();
                return;
            }

            // Day 13-B: prefer the ingredient sprite over the text label
            // (label was readable but it crashed into the world-space
            // monologue bubble at scale 1.5). The text fallback fires
            // only when no sprite is wired up for the ingredient type.
            var sprite = ResolveIngredientSprite(type);
            var hasSprite = sprite != null && _heldItemSr != null;
            if (hasSprite)
            {
                _heldItemSr.sprite = sprite;
                _heldItemSr.gameObject.SetActive(true);
            }
            else if (_heldItemSr != null)
            {
                _heldItemSr.gameObject.SetActive(false);
            }

            if (_heldLabel == null) return;
            if (hasSprite)
            {
                _heldLabel.text = string.Empty;
                _heldLabel.gameObject.SetActive(false);
                return;
            }
            var def = kitchen.GetDefinition(type);
            var name = def != null && !string.IsNullOrWhiteSpace(def.DisplayName)
                ? def.DisplayName
                : type.ToString();
            _heldLabel.text = $"손에: {name}";
            _heldLabel.gameObject.SetActive(true);
        }

        private Sprite ResolveIngredientSprite(IngredientType type)
        {
            if (_ingredientSprites == null) return null;
            for (var i = 0; i < _ingredientSprites.Length; i++)
            {
                if (_ingredientSprites[i].type == type) return _ingredientSprites[i].sprite;
            }
            return null;
        }

        private void ClearHeldLabel()
        {
            if (_heldLabel != null)
            {
                _heldLabel.text = string.Empty;
                _heldLabel.gameObject.SetActive(false);
            }
            if (_heldItemSr != null)
            {
                _heldItemSr.gameObject.SetActive(false);
            }
        }

        public void ResetVisuals()
        {
            if (_current != null)
            {
                StopCoroutine(_current);
                _current = null;
            }
            transform.position = _homePosition;
            _displayedHeld = null;
            ClearHeldLabel();
            HideThinking();
        }

        public void ShowThinking()
        {
            if (_thinkingRoot == null) return;
            _thinkingRoot.SetActive(true);
            if (_thinkingLoop != null) StopCoroutine(_thinkingLoop);
            _thinkingLoop = StartCoroutine(ThinkingDots());
        }

        public void HideThinking()
        {
            if (_thinkingLoop != null)
            {
                StopCoroutine(_thinkingLoop);
                _thinkingLoop = null;
            }
            if (_thinkingRoot != null) _thinkingRoot.SetActive(false);
        }

        private IEnumerator ThinkingDots()
        {
            var i = 0;
            // Cycle "·" → "··" → "···" so the chef visibly "thinks" while
            // the Gemini call is in flight (~2-4s). Center-dot glyph
            // reads as a thought-bubble in pixel font.
            var frames = new[] { "·", "··", "···" };
            while (true)
            {
                if (_thinkingText != null) _thinkingText.text = frames[i % frames.Length];
                i++;
                yield return new WaitForSeconds(0.35f);
            }
        }

        // Editor-only helper so MainKitchenSetup can wire the held-label
        // child without going through SerializedObject for one ref.
        public void SetHeldLabel(TextMeshPro label) => _heldLabel = label;
    }
}
