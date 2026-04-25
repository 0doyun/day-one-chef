// Round outcome published by Unity over the bridge and appended to the
// session's results log. Schema mirrors BridgeMessage.RoundEnd —
// Unity already sends orderTitle / roundIndex / totalRounds /
// instruction, so the result log UI can render rich Korean rows
// without a second round-trip.

class GameResult {
  const GameResult({
    required this.orderId,
    required this.orderTitle,
    required this.roundIndex,
    required this.totalRounds,
    required this.instruction,
    required this.success,
    required this.reason,
    required this.receivedAt,
  });

  final String orderId;
  final String orderTitle;
  final int roundIndex;
  final int totalRounds;
  final String instruction;
  final bool success;
  final String reason;
  final DateTime receivedAt;

  @override
  String toString() => 'GameResult($orderId, success=$success, reason="$reason")';
}
