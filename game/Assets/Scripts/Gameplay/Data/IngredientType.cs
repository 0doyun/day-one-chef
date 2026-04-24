// The 6 ingredient types the player starts a session with — see
// design/gdd/game-concept.md §3.2. Order is stable: new ingredients are
// appended (never reordered) so serialised ScriptableObject references
// are safe across versions.

namespace DayOneChef.Gameplay.Data
{
    public enum IngredientType
    {
        Patty,   // 패티
        Bread,   // 빵
        Cheese,  // 치즈
        Lettuce, // 상추
        Tomato,  // 토마토
        Egg,     // 계란
    }
}
