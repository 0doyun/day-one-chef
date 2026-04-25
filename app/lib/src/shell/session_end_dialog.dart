// Modal that pops the moment the last round's evaluator verdict lands
// (sessionSummaryProvider transitions from null → non-null). Without
// it, the session just trails off — the result log's header changes
// to "세션 완료 N/5" but the player has no clear "GAME OVER" beat.
//
// Mounted from unity_host_page.dart via `ref.listen`, not from inside
// the SidePanel — the dialog covers the Unity canvas and the player
// must explicitly act ("내일 다시" or close) before the next session.

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../game/models/game_result.dart';
import '../game/providers.dart';
import 'flutter_bridge.dart';

class SessionEndDialog extends ConsumerWidget {
  const SessionEndDialog({super.key, required this.bridge});

  final FlutterBridge bridge;

  static Future<void> show(BuildContext context, FlutterBridge bridge) {
    return showDialog<void>(
      context: context,
      barrierDismissible: false,
      builder: (_) => SessionEndDialog(bridge: bridge),
    );
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final summary = ref.watch(sessionSummaryProvider);
    final results = ref.watch(gameResultsProvider);

    return Dialog(
      backgroundColor: const Color(0xFF1F1612),
      insetPadding: const EdgeInsets.symmetric(horizontal: 40, vertical: 60),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 460),
        child: Padding(
          padding: const EdgeInsets.fromLTRB(28, 24, 28, 20),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Row(
                children: [
                  Icon(Icons.nightlight_round,
                      color: Color(0xFFF2C84B), size: 22),
                  SizedBox(width: 8),
                  Text(
                    '오늘 영업 끝!',
                    style: TextStyle(
                      color: Colors.white,
                      fontSize: 22,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 14),
              _Stats(summary: summary),
              const SizedBox(height: 16),
              if (results.isNotEmpty) ...[
                const Text(
                  '오늘의 라운드',
                  style: TextStyle(
                    color: Colors.white60,
                    fontSize: 11,
                    fontWeight: FontWeight.w600,
                    letterSpacing: 1.2,
                  ),
                ),
                const SizedBox(height: 8),
                Flexible(
                  child: SingleChildScrollView(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        for (final r in results) _ResultLine(r: r),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: 16),
              ],
              Row(
                children: [
                  Expanded(
                    child: TextButton(
                      onPressed: () => Navigator.of(context).pop(),
                      style: TextButton.styleFrom(
                        foregroundColor: Colors.white70,
                        padding: const EdgeInsets.symmetric(vertical: 14),
                      ),
                      child: const Text('닫기',
                          style: TextStyle(fontSize: 14)),
                    ),
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    flex: 2,
                    child: ElevatedButton.icon(
                      onPressed: () {
                        ref.read(gameResultsProvider.notifier).clear();
                        ref.read(sessionSummaryProvider.notifier).state = null;
                        ref.read(currentOrderProvider.notifier).state = null;
                        bridge.sendRestartSession();
                        Navigator.of(context).pop();
                      },
                      icon: const Icon(Icons.restart_alt, size: 18),
                      style: ElevatedButton.styleFrom(
                        backgroundColor: const Color(0xFFF2C84B),
                        foregroundColor: const Color(0xFF1F140A),
                        padding: const EdgeInsets.symmetric(vertical: 14),
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(8),
                        ),
                      ),
                      label: const Text('내일 다시',
                          style: TextStyle(
                              fontSize: 14, fontWeight: FontWeight.w700)),
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _Stats extends StatelessWidget {
  const _Stats({required this.summary});

  final SessionSummary? summary;

  @override
  Widget build(BuildContext context) {
    if (summary == null) {
      return const SizedBox.shrink();
    }
    final s = summary!;
    final allPass = s.failCount == 0 && s.successCount > 0;
    return DecoratedBox(
      decoration: BoxDecoration(
        color: Colors.white.withValues(alpha: 0.04),
        borderRadius: BorderRadius.circular(8),
        border: Border(
          left: BorderSide(
            color: allPass
                ? const Color(0xFFF2C84B)
                : const Color(0xFF7BC67B),
            width: 3,
          ),
        ),
      ),
      child: Padding(
        padding: const EdgeInsets.fromLTRB(14, 10, 14, 10),
        child: Row(
          children: [
            Text(
              '${s.successCount} / ${s.totalRounds} 성공',
              style: const TextStyle(
                color: Colors.white,
                fontSize: 18,
                fontWeight: FontWeight.w700,
              ),
            ),
            const Spacer(),
            if (s.failCount > 0)
              Text(
                '실패 ${s.failCount}',
                style: const TextStyle(
                  color: Color(0xFFE07A6E),
                  fontSize: 13,
                  fontWeight: FontWeight.w600,
                ),
              )
            else
              const Text(
                '🏆 완벽한 하루',
                style: TextStyle(
                  color: Color(0xFFF2C84B),
                  fontSize: 13,
                  fontWeight: FontWeight.w600,
                ),
              ),
          ],
        ),
      ),
    );
  }
}

class _ResultLine extends StatelessWidget {
  const _ResultLine({required this.r});

  final GameResult r;

  @override
  Widget build(BuildContext context) {
    final accent = r.success
        ? const Color(0xFF7BC67B)
        : const Color(0xFFE07A6E);
    final title = r.orderTitle.isNotEmpty
        ? r.orderTitle
        : (r.orderId.isNotEmpty ? r.orderId : '주문 미상');
    return Padding(
      padding: const EdgeInsets.only(bottom: 6),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(
            r.success ? Icons.check_circle : Icons.cancel,
            color: accent,
            size: 14,
          ),
          const SizedBox(width: 8),
          SizedBox(
            width: 70,
            child: Text(
              title,
              style: const TextStyle(
                color: Colors.white,
                fontSize: 12,
                fontWeight: FontWeight.w600,
              ),
              overflow: TextOverflow.ellipsis,
            ),
          ),
          const SizedBox(width: 8),
          Expanded(
            child: Text(
              r.reason,
              style: TextStyle(
                color: Colors.white.withValues(alpha: 0.7),
                fontSize: 12,
                height: 1.35,
              ),
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
            ),
          ),
        ],
      ),
    );
  }
}
