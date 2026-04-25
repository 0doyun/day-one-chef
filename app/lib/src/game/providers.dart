// Riverpod providers for the Day 8 shell. Exposes:
//   - `sessionProvider`: per-session metadata (start time, etc.).
//   - `gameResultsProvider`: accumulated round outcomes.
//
// Day 9 wires these to the Unity→Flutter bridge so incoming `round_end`
// messages call `append`; Day 13 surfaces them in a result log UI.

import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'bridge_message.dart' show OrderComponent;
import 'models/game_result.dart';

class SessionInfo {
  SessionInfo({required this.startedAt});
  final DateTime startedAt;
}

final sessionProvider = StateProvider<SessionInfo>((ref) {
  return SessionInfo(startedAt: DateTime.now());
});

final gameResultsProvider =
    StateNotifierProvider<GameResultsNotifier, List<GameResult>>((ref) {
  return GameResultsNotifier();
});

class SessionSummary {
  const SessionSummary({
    required this.successCount,
    required this.failCount,
    required this.totalRounds,
  });

  final int successCount;
  final int failCount;
  final int totalRounds;
}

final sessionSummaryProvider = StateProvider<SessionSummary?>((ref) => null);

class CurrentOrder {
  const CurrentOrder({
    required this.orderId,
    required this.recipeName,
    required this.roundIndex,
    required this.totalRounds,
    required this.components,
  });

  final String orderId;
  final String recipeName;
  final int roundIndex;
  final int totalRounds;
  final List<OrderComponent> components;
}

final currentOrderProvider = StateProvider<CurrentOrder?>((ref) => null);

class GameResultsNotifier extends StateNotifier<List<GameResult>> {
  GameResultsNotifier() : super(const []);

  void append(GameResult result) {
    state = [...state, result];
  }

  void clear() {
    state = const [];
  }

  void removeLast() {
    if (state.isEmpty) return;
    state = state.sublist(0, state.length - 1);
  }
}
