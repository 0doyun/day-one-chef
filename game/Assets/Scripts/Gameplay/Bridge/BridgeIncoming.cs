// JS → Unity inbound bridge. Flutter sends messages by running
//   unityInstance.SendMessage('BridgeReceiver', '<MethodName>', '<payload>');
// which Unity dispatches to this MonoBehaviour's public methods.
//
// GameRound subscribes to the static events on Awake so gameplay
// logic stays decoupled from the bridge plumbing — the receiver just
// broadcasts intents, listeners decide what to do.

using UnityEngine;

namespace DayOneChef.Bridge
{
    public class BridgeIncoming : MonoBehaviour
    {
        public static event System.Action OnResetRoundRequested;
        public static event System.Action OnSessionRestartRequested;

        /// <summary>Flutter asked us to replay the current round.</summary>
        public void ResetRound()
        {
            Debug.Log("[BridgeIncoming] ResetRound — reply from Flutter");
            OnResetRoundRequested?.Invoke();
        }

        /// <summary>Flutter asked us to restart the whole 5-round session.</summary>
        public void RestartSession()
        {
            Debug.Log("[BridgeIncoming] RestartSession — reply from Flutter");
            OnSessionRestartRequested?.Invoke();
        }
    }
}
