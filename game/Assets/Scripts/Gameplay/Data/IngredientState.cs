// Unified ingredient state enum. Each ingredient type permits a subset
// of these — enforced by IngredientDefinition.AllowedStates at runtime.
// See design/gdd/game-concept.md §3.2 for the per-ingredient state map
// (패티: 안익음 → 익음 → 탐, 치즈: 통째 → 슬라이스, etc.).

namespace DayOneChef.Gameplay.Data
{
    public enum IngredientState
    {
        Raw,      // 패티·빵 기본 상태 (안익음 / 일반)
        Cooked,   // 패티·빵 구움, 계란찜/오믈렛 완성
        Burnt,    // 탐
        Whole,    // 치즈·상추·토마토 통째
        Sliced,   // 치즈 슬라이스
        Chopped,  // 상추·토마토 썰기 완료
        Washed,   // 상추 씻음
        Shell,    // 계란 껍질 상태
        Cracked,  // 계란 깨짐 (노른자/흰자 분리 안 섞임)
        Beaten,   // 계란 풀림
        Mixed,    // 계란찜용 — 계란 + 물 + 소금 혼합
    }
}
