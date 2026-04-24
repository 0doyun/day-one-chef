// Per-ingredient definition asset. Lists the display name and the full
// set of states the ingredient can occupy — any state transition
// attempted outside this set is rejected by Ingredient.TrySetState.
//
// Assets are generated under Assets/Data/Ingredients/ by the editor
// script DayOneChef.Editor.GameDataGenerator.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace DayOneChef.Gameplay.Data
{
    [CreateAssetMenu(
        menuName = "Day One Chef/Ingredient Definition",
        fileName = "IngredientDefinition")]
    public class IngredientDefinition : ScriptableObject
    {
        [SerializeField] private IngredientType _type;
        [SerializeField] private string _displayName;
        [SerializeField] private IngredientState _initialState;
        [SerializeField] private IngredientState[] _allowedStates = Array.Empty<IngredientState>();

        public IngredientType Type => _type;
        public string DisplayName => _displayName;
        public IngredientState InitialState => _initialState;
        public IReadOnlyList<IngredientState> AllowedStates => _allowedStates;

        public bool Allows(IngredientState state)
        {
            if (_allowedStates == null) return false;
            for (var i = 0; i < _allowedStates.Length; i++)
            {
                if (_allowedStates[i] == state) return true;
            }
            return false;
        }

        public void Configure(
            IngredientType type,
            string displayName,
            IngredientState initialState,
            IngredientState[] allowedStates)
        {
            _type = type;
            _displayName = displayName ?? string.Empty;
            _initialState = initialState;
            _allowedStates = allowedStates ?? Array.Empty<IngredientState>();
        }
    }
}
