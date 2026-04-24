// Gemini call #1 boundary. Day 5 ships a direct-from-WebGL
// implementation (GeminiClient); Day 8-9 adds a FlutterProxyGeminiClient
// so production builds don't ship the API key. Gameplay code depends on
// this interface so swapping implementations doesn't touch GameRound,
// the action executor, or any HUD consumers. See ADR-0003.

using System.Threading;
using System.Threading.Tasks;
using DayOneChef.Gameplay.Data;

namespace DayOneChef.Gameplay.AI
{
    public interface IGeminiClient
    {
        Task<ChefActionResponse> GenerateActionsAsync(
            GameStateSnapshot state,
            string playerInstruction,
            CancellationToken ct = default);
    }
}
