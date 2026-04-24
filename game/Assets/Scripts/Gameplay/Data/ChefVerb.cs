// The 8 verbs Gemini is allowed to emit in its action array. Locked to
// match design/gdd/game-concept.md §4.1 — changes here must be
// reflected in the system prompt and the prompt-builder tests.

namespace DayOneChef.Gameplay.Data
{
    public enum ChefVerb
    {
        Pickup,   // 재료/결과물 집기
        Cook,     // 화구 (param: grill | fry | steam)
        Chop,     // 도마
        Crack,    // 계란 깨기
        Mix,      // 그릇 섞기
        Assemble, // 조립대
        Serve,    // 카운터
        Move,     // 위치 이동
    }
}
