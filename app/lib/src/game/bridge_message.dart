// Dart mirror of Unity's BridgeMessage wire format.
// Keep field names in sync with game/Assets/Scripts/Bridge/BridgeMessage.cs —
// JsonUtility serialises C# public fields verbatim, so any rename
// breaks both sides silently. See ADR-0002 for the message-type table.

import 'dart:convert';

enum BridgeType {
  roundEnd('round_end'),
  sessionEnd('session_end'),
  orderPresent('order_present'),
  consoleLog('console_log'),
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
    this.recipeName = '',
    this.components = const [],
    this.level = '',
    this.text = '',
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
  // `order_present` payload — Flutter renders the right-panel recipe view
  // from these instead of Unity's old on-canvas OrderCard.
  final String recipeName;
  final List<OrderComponent> components;
  // Debug forwarding (console_log) — populated by the index.html shim.
  final String level;
  final String text;

  factory BridgeMessage.fromJsonString(String raw) {
    final Map<String, dynamic> m = json.decode(raw) as Map<String, dynamic>;
    final compsRaw = m['components'];
    final components = <OrderComponent>[];
    if (compsRaw is List) {
      for (final c in compsRaw) {
        if (c is Map) components.add(OrderComponent.fromMap(c.cast<String, dynamic>()));
      }
    }
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
      recipeName: (m['recipeName'] as String?) ?? '',
      components: components,
      level: (m['level'] as String?) ?? '',
      text: (m['text'] as String?) ?? '',
    );
  }
}

class OrderComponent {
  const OrderComponent({
    required this.type,
    required this.state,
    required this.typeKr,
    required this.stateKr,
  });

  final String type;
  final String state;
  final String typeKr;
  final String stateKr;

  factory OrderComponent.fromMap(Map<String, dynamic> m) => OrderComponent(
        type: (m['type'] as String?) ?? '',
        state: (m['state'] as String?) ?? '',
        typeKr: (m['typeKr'] as String?) ?? '',
        stateKr: (m['stateKr'] as String?) ?? '',
      );
}
