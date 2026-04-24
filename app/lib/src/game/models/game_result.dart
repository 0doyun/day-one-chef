// Round outcome published by Unity over the bridge and appended to the
// session's results log. Wire schema is intentionally minimal for
// Day 8 — the evaluator (Day 11) will fill in richer reason strings
// and failure categories.

class GameResult {
  const GameResult({
    required this.orderId,
    required this.success,
    required this.reason,
    required this.receivedAt,
  });

  final String orderId;
  final bool success;
  final String reason;
  final DateTime receivedAt;

  factory GameResult.fromBridgeJson(Map<String, dynamic> json) {
    return GameResult(
      orderId: (json['orderId'] as String?) ?? '',
      success: (json['success'] as bool?) ?? false,
      reason: (json['reason'] as String?) ?? '',
      receivedAt: DateTime.now(),
    );
  }

  @override
  String toString() => 'GameResult($orderId, success=$success, reason="$reason")';
}
