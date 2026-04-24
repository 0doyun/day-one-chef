// Plain-data snapshot of what Gemini needs to reason about the current
// round — current order, available ingredients, available stations.
// Passed into GeminiPromptBuilder so prompt construction stays free of
// scene references and is unit-testable.

using System.Collections.Generic;

namespace DayOneChef.Gameplay.AI
{
    public readonly struct GameStateSnapshot
    {
        public string OrderDisplayName { get; }
        public string OrderExampleInstruction { get; }
        public IReadOnlyList<string> AvailableIngredients { get; }
        public IReadOnlyList<string> AvailableStations { get; }

        public GameStateSnapshot(
            string orderDisplayName,
            string orderExampleInstruction,
            IReadOnlyList<string> availableIngredients,
            IReadOnlyList<string> availableStations)
        {
            OrderDisplayName = orderDisplayName ?? string.Empty;
            OrderExampleInstruction = orderExampleInstruction ?? string.Empty;
            AvailableIngredients = availableIngredients ?? System.Array.Empty<string>();
            AvailableStations = availableStations ?? System.Array.Empty<string>();
        }
    }
}
