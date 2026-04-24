// Live ingredient state for the current round. Spawns every ingredient
// at its IngredientDefinition.InitialState ("원시 상태" per GDD §3.2)
// and tracks transitions as the ActionExecutor pumps verbs.
//
// Used by:
//  - ActionExecutor — mutates on pickup/cook/chop/crack/mix
//  - Day 11 evaluator — reads the final map + event log for judgement
//
// Resolution tables (Korean display name → enum) are built once from
// the same IngredientDefinition / StationMarker assets the rest of the
// game uses, so Gemini-emitted targets like "빵" or "화구" resolve
// without a parallel hardcoded dictionary.

using System.Collections.Generic;
using UnityEngine;
using DayOneChef.Gameplay.Data;

namespace DayOneChef.Gameplay
{
    public class KitchenState
    {
        private readonly Dictionary<IngredientType, IngredientDefinition> _defs = new();
        private readonly Dictionary<IngredientType, IngredientState> _state = new();
        private readonly Dictionary<string, IngredientType> _nameToIngredient = new();
        private readonly Dictionary<string, StationType> _nameToStation = new();

        public IngredientType? ChefHolding { get; private set; }
        public IReadOnlyDictionary<IngredientType, IngredientState> State => _state;

        public KitchenState(
            IEnumerable<IngredientDefinition> ingredientDefs,
            IEnumerable<StationMarker> stations)
        {
            foreach (var def in ingredientDefs)
            {
                if (def == null) continue;
                _defs[def.Type] = def;
                _state[def.Type] = def.InitialState;
                RegisterName(_nameToIngredient, def.DisplayName, def.Type);
                RegisterName(_nameToIngredient, def.Type.ToString(), def.Type);
            }
            foreach (var station in stations)
            {
                if (station == null) continue;
                RegisterName(_nameToStation, station.DisplayLabel, station.StationType);
                RegisterName(_nameToStation, station.StationType.ToString(), station.StationType);
            }
        }

        private static void RegisterName<TEnum>(IDictionary<string, TEnum> map, string raw, TEnum value)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;
            map[raw.Trim().ToLowerInvariant()] = value;
        }

        public bool TryResolveIngredient(string raw, out IngredientType type)
            => _nameToIngredient.TryGetValue((raw ?? string.Empty).Trim().ToLowerInvariant(), out type);

        public bool TryResolveStation(string raw, out StationType type)
            => _nameToStation.TryGetValue((raw ?? string.Empty).Trim().ToLowerInvariant(), out type);

        public IngredientState GetState(IngredientType type) =>
            _state.TryGetValue(type, out var s) ? s : default;

        public IngredientDefinition GetDefinition(IngredientType type) =>
            _defs.TryGetValue(type, out var d) ? d : null;

        public bool SetHolding(IngredientType? type)
        {
            ChefHolding = type;
            return true;
        }

        /// <summary>
        /// Apply a state transition if the target ingredient permits it.
        /// Returns true on success, false if the transition is disallowed
        /// (which causes the executor to mark the event as skipped).
        /// </summary>
        public bool TrySetState(IngredientType type, IngredientState next, out string reason)
        {
            reason = null;
            if (!_defs.TryGetValue(type, out var def))
            {
                reason = $"no IngredientDefinition for {type}";
                return false;
            }
            if (!def.Allows(next))
            {
                reason = $"{type} does not permit {next}";
                return false;
            }
            _state[type] = next;
            return true;
        }

        public string DumpFinalState()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var kv in _state)
            {
                sb.Append(kv.Key).Append('=').Append(kv.Value).Append(' ');
            }
            return sb.ToString().TrimEnd();
        }
    }
}
