// Runtime ingredient instance. Holds a reference to its definition
// asset (source of truth for allowed states) and its current state.
// State transitions go through TrySetState so illegal transitions
// (e.g. Cheese → Cooked) are rejected with a warning rather than
// corrupting the run.

using System;
using UnityEngine;
using DayOneChef.Gameplay.Data;

namespace DayOneChef.Gameplay
{
    public class Ingredient : MonoBehaviour
    {
        [SerializeField] private IngredientDefinition _definition;
        [SerializeField] private IngredientState _currentState;

        public event Action<Ingredient, IngredientState, IngredientState> StateChanged;

        public IngredientDefinition Definition => _definition;
        public IngredientType Type => _definition != null ? _definition.Type : default;
        public IngredientState CurrentState => _currentState;

        public void Configure(IngredientDefinition definition)
        {
            _definition = definition;
            _currentState = definition != null ? definition.InitialState : default;
        }

        public bool TrySetState(IngredientState next)
        {
            if (_definition == null)
            {
                Debug.LogWarning($"[Ingredient] {name} has no IngredientDefinition — state change to {next} rejected.");
                return false;
            }
            if (!_definition.Allows(next))
            {
                Debug.LogWarning(
                    $"[Ingredient] {_definition.Type} does not permit state {next}. " +
                    "Check the recipe or the action being executed.");
                return false;
            }
            var prev = _currentState;
            if (prev == next) return true;
            _currentState = next;
            StateChanged?.Invoke(this, prev, next);
            return true;
        }
    }
}
