// Polish-tier hook the executor calls after every applied action so a
// view layer (a MonoBehaviour, normally) can play movement, bobs, and
// held-ingredient updates. Defined as an interface so EditMode tests
// keep building ActionExecutor without dragging in scene state.

using DayOneChef.Gameplay.Data;

namespace DayOneChef.Gameplay
{
    public interface IChefAnimator
    {
        /// <summary>
        /// Called once per executor tick with the just-applied event.
        /// Implementations must return immediately; long-running motion
        /// runs on the implementor's own scheduler (e.g. coroutine) so
        /// the executor's ACTION_TICK budget is unaffected.
        /// </summary>
        void OnAction(ChefVerb verb, EventLogEntry entry, KitchenState kitchen);
    }
}
