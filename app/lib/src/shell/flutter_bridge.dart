// Flutter side of the Unity ↔ Flutter bridge.
//
// Registers the `FlutterBridge` JavaScript channel on a
// WebViewController, parses incoming Unity messages, and routes them
// to Riverpod. Outbound calls (Flutter → Unity) are convenience
// wrappers around `controller.runJavaScript("unityInstance.
// SendMessage('BridgeReceiver', 'Method', '')")` — see
// docs/architecture/ADR-0002-bridge-message-protocol.md.

import 'package:flutter/foundation.dart' show debugPrint;
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:webview_flutter/webview_flutter.dart';

import '../game/bridge_message.dart';
import '../game/models/game_result.dart';
import '../game/providers.dart';

class FlutterBridge {
  FlutterBridge({required this.ref, required this.controller});

  // `WidgetRef` (not `Ref`) — the bridge is owned by a widget state,
  // and `read` / `watch` live on the widget-scoped Ref. Keeping the
  // type concrete stops anyone from instantiating this from a pure
  // provider-build function where the assumption would be wrong.
  final WidgetRef ref;
  final WebViewController controller;

  static const String _channelName = 'FlutterBridge';
  static const String _receiverObject = 'BridgeReceiver';

  void attach() {
    controller.addJavaScriptChannel(
      _channelName,
      onMessageReceived: (JavaScriptMessage msg) => _onIncoming(msg.message),
    );
  }

  void _onIncoming(String raw) {
    try {
      final message = BridgeMessage.fromJsonString(raw);
      // Console log forwarding stays out of the round-result router
      // so it doesn't pollute the result log; print it raw and stop.
      if (message.type == BridgeType.consoleLog) {
        debugPrint('[Unity ${message.level}] ${message.text}');
        return;
      }
      debugPrint('[FlutterBridge] <- Unity: $raw');
      switch (message.type) {
        case BridgeType.roundEnd:
          _handleRoundEnd(message);
          break;
        case BridgeType.sessionEnd:
          ref.read(sessionSummaryProvider.notifier).state = SessionSummary(
            successCount: message.successCount,
            failCount: message.failCount,
            totalRounds: message.totalRounds,
          );
          break;
        case BridgeType.consoleLog:
          break; // handled above
        case BridgeType.unknown:
          debugPrint('[FlutterBridge] unknown message type; dropped.');
      }
    } on FormatException catch (e) {
      debugPrint('[FlutterBridge] malformed payload: $e');
    }
  }

  void _handleRoundEnd(BridgeMessage m) {
    final result = GameResult(
      orderId: m.orderId,
      orderTitle: m.orderTitle,
      roundIndex: m.roundIndex,
      totalRounds: m.totalRounds,
      instruction: m.instruction,
      success: m.success,
      reason: m.reason,
      receivedAt: DateTime.now(),
    );
    ref.read(gameResultsProvider.notifier).append(result);
  }

  // Outbound — Flutter → Unity ------------------------------------------

  Future<void> sendResetRound() => _sendToUnity('ResetRound');
  Future<void> sendRestartSession() => _sendToUnity('RestartSession');

  Future<void> _sendToUnity(String method, [String payload = '']) async {
    // The JS bootstrap in the Unity template exposes
    // `window.unityInstance` once the loader resolves.
    final escapedPayload = payload.replaceAll("'", "\\'");
    final script =
        "if (window.unityInstance) { unityInstance.SendMessage('$_receiverObject', '$method', '$escapedPayload'); } "
        "else { console.warn('[FlutterBridge] unityInstance not ready for $method'); }";
    await controller.runJavaScript(script);
  }
}
