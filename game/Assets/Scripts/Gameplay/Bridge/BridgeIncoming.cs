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
        public static event System.Action<string> OnSubmitInstructionRequested;

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

        /// <summary>
        /// Flutter typed a Korean 지시 and pressed send. We pass the raw
        /// text up to <see cref="GameRound"/> via the static event; the
        /// chef will respond as if the player typed it in-engine. macOS
        /// WKWebView's IME composition was broken when routed through
        /// Unity's TMP_InputField, so the input lives on the Flutter
        /// side and arrives here pre-composed.
        /// </summary>
        public void SubmitInstruction(string instruction)
        {
            Debug.Log($"[BridgeIncoming] SubmitInstruction — \"{instruction}\"");
            OnSubmitInstructionRequested?.Invoke(instruction);
        }
    }
}
