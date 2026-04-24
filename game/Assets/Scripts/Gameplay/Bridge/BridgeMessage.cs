// Wire format for Unity ↔ Flutter bridge messages. Kept as a single
// flat Serializable — Unity's JsonUtility does not support polymorphic
// type dispatch, and a "fields used per type" layout avoids the
// round-trip hazards of manually parsing Newtonsoft JObject trees on
// both sides. See ADR-0002 for the message-type table and the
// direction-of-authority rules.
//
// Day 9 ships `round_end` (Unity → Flutter) and `session_end`
// (Unity → Flutter). Flutter → Unity messages do not use this
// struct; they route through `unityInstance.SendMessage(gameObject,
// method, payload)` and arrive at `BridgeIncoming` MonoBehaviour
// handlers directly.

using System;

namespace DayOneChef.Bridge
{
    [Serializable]
    public class BridgeMessage
    {
        /// <summary>Discriminator — "round_end", "session_end", etc.</summary>
        public string type;

        // `round_end` fields -------------------------------------------------
        public string orderId;
        public string orderTitle;
        public int roundIndex;      // 0-based
        public int totalRounds;
        public string instruction;  // the player's 한글 text this round
        public bool success;        // filled by the Day 11 evaluator; Day 9 emits `false` + reason="pending evaluator"
        public string reason;
        public string eventLogJson; // serialized EventLog.ToJson() — opaque to Flutter for now

        // `session_end` extras -----------------------------------------------
        public int successCount;
        public int failCount;

        public static BridgeMessage RoundEnd(
            string orderId, string orderTitle, int roundIndex, int totalRounds,
            string instruction, bool success, string reason, string eventLogJson)
        {
            return new BridgeMessage
            {
                type = "round_end",
                orderId = orderId,
                orderTitle = orderTitle,
                roundIndex = roundIndex,
                totalRounds = totalRounds,
                instruction = instruction,
                success = success,
                reason = reason,
                eventLogJson = eventLogJson,
            };
        }

        public static BridgeMessage SessionEnd(int successCount, int failCount, int totalRounds)
        {
            return new BridgeMessage
            {
                type = "session_end",
                successCount = successCount,
                failCount = failCount,
                totalRounds = totalRounds,
            };
        }
    }
}
