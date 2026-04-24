// Customer mood sprites. Non-goals per GDD §9 (no timer, no patience
// mechanic) mean mood is presentation-only for Day 4 — no gameplay
// effect yet. Future Day 12 tuning may tie mood to round outcome.

namespace DayOneChef.Gameplay.Data
{
    public enum CustomerMood
    {
        Bored,   // 심심해하는
        Waiting, // 기다리는
        Angry,   // 화난
    }
}
