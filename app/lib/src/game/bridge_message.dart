// Dart mirror of Unity's BridgeMessage wire format.
// Keep field names in sync with game/Assets/Scripts/Bridge/BridgeMessage.cs —
// JsonUtility serialises C# public fields verbatim, so any rename
// breaks both sides silently. See ADR-0002 for the message-type table.

import 'dart:convert';

enum BridgeType {
  roundEnd('round_end'),
  sessionEnd('session_end'),
  unknown('');

  const BridgeType(this.wire);
  final String wire;

  static BridgeType parse(String? raw) {
    for (final t in BridgeType.values) {
      if (t.wire == raw) return t;
    }
    return BridgeType.unknown;
  }
}

class BridgeMessage {
  const BridgeMessage({
    required this.type,
    this.orderId = '',
    this.orderTitle = '',
    this.roundIndex = 0,
    this.totalRounds = 0,
    this.instruction = '',
    this.success = false,
    this.reason = '',
    this.eventLogJson = '',
    this.successCount = 0,
    this.failCount = 0,
  });

  final BridgeType type;
  final String orderId;
  final String orderTitle;
  final int roundIndex;
  final int totalRounds;
  final String instruction;
  final bool success;
  final String reason;
  final String eventLogJson;
  final int successCount;
  final int failCount;

  factory BridgeMessage.fromJsonString(String raw) {
    final Map<String, dynamic> m = json.decode(raw) as Map<String, dynamic>;
    return BridgeMessage(
      type: BridgeType.parse(m['type'] as String?),
      orderId: (m['orderId'] as String?) ?? '',
      orderTitle: (m['orderTitle'] as String?) ?? '',
      roundIndex: (m['roundIndex'] as int?) ?? 0,
      totalRounds: (m['totalRounds'] as int?) ?? 0,
      instruction: (m['instruction'] as String?) ?? '',
      success: (m['success'] as bool?) ?? false,
      reason: (m['reason'] as String?) ?? '',
      eventLogJson: (m['eventLogJson'] as String?) ?? '',
      successCount: (m['successCount'] as int?) ?? 0,
      failCount: (m['failCount'] as int?) ?? 0,
    );
  }
}
