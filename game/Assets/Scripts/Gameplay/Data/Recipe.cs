// Recipe — ordered list of ingredient requirements plus an order-
// sensitivity flag. The evaluator on Day 11 reads the requirements;
// the order-sensitive flag drives event_log verification (per GDD §4.2
// for 계란찜).
//
// For Day 4 prototype, Matches() does a shallow type+state comparison
// against a final ingredient list. The deeper order-sensitive path
// (comparing event_log action order) lands on Day 6/11.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace DayOneChef.Gameplay.Data
{
    [Serializable]
    public struct RecipeComponent
    {
        public IngredientType Type;
        public IngredientState RequiredState;
    }

    [CreateAssetMenu(
        menuName = "Day One Chef/Recipe",
        fileName = "Recipe")]
    public class Recipe : ScriptableObject
    {
        [SerializeField] private string _displayName;
        [SerializeField] private RecipeComponent[] _components = Array.Empty<RecipeComponent>();
        [SerializeField] private bool _orderSensitive;
        // Procedure notes — Korean-language description of the cooking
        // technique that distinguishes this dish from siblings. Read
        // by the evaluator prompt so Gemini can verify the event_log's
        // verb sequence, not just the final ingredient state. Without
        // this, "계란 익혀" passed both 오믈렛 and 계란찜 (both end with
        // egg in state Cooked) since Components alone can't capture
        // whether the egg went through Mix.
        [SerializeField, TextArea(2, 4)] private string _procedureNotes;

        public string DisplayName => _displayName;
        public IReadOnlyList<RecipeComponent> Components => _components;
        public bool OrderSensitive => _orderSensitive;
        public string ProcedureNotes => _procedureNotes ?? string.Empty;

        public void Configure(string displayName, RecipeComponent[] components,
                               bool orderSensitive, string procedureNotes = null)
        {
            _displayName = displayName ?? string.Empty;
            _components = components ?? Array.Empty<RecipeComponent>();
            _orderSensitive = orderSensitive;
            _procedureNotes = procedureNotes ?? string.Empty;
        }

        /// <summary>
        /// Shallow match — every required component must exist (by type +
        /// state) in <paramref name="finalIngredients"/>, consumed once.
        /// Does NOT check the order of preparation; order-sensitive recipes
        /// additionally need event_log verification, added on Day 6.
        /// </summary>
        public bool Matches(IReadOnlyList<(IngredientType Type, IngredientState State)> finalIngredients)
        {
            if (finalIngredients == null) return false;
            if (_components == null || _components.Length == 0) return finalIngredients.Count == 0;

            var consumed = new bool[finalIngredients.Count];
            for (var i = 0; i < _components.Length; i++)
            {
                var required = _components[i];
                var matched = false;
                for (var j = 0; j < finalIngredients.Count; j++)
                {
                    if (consumed[j]) continue;
                    var actual = finalIngredients[j];
                    if (actual.Type == required.Type && actual.State == required.RequiredState)
                    {
                        consumed[j] = true;
                        matched = true;
                        break;
                    }
                }
                if (!matched) return false;
            }
            return true;
        }
    }
}
