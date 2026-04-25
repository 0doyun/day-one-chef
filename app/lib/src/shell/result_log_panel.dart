// Right-edge overlay that surfaces the session's accumulated round
// outcomes. Reads `gameResultsProvider` for the per-round list and
// `sessionSummaryProvider` for the final tally. The Unity canvas
// stays visible underneath; the panel is a translucent strip so the
// kitchen action is never fully occluded.

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../game/models/game_result.dart';
import '../game/providers.dart';
import 'flutter_bridge.dart';

class ResultLogPanel extends ConsumerWidget {
  const ResultLogPanel({super.key, required this.bridge});

  final FlutterBridge? bridge;

  static const double width = 300;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final br = bridge;
    if (br == null) return const SizedBox.shrink();
    final results = ref.watch(gameResultsProvider);
    final summary = ref.watch(sessionSummaryProvider);

    return SizedBox(
      width: width,
      child: DecoratedBox(
        decoration: BoxDecoration(
          color: Colors.black.withValues(alpha: 0.62),
          borderRadius: const BorderRadius.only(
            topLeft: Radius.circular(12),
            bottomLeft: Radius.circular(12),
          ),
          border: Border.all(color: Colors.white.withValues(alpha: 0.08)),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            _Header(results: results, summary: summary),
            const Divider(height: 1, color: Colors.white12),
            Expanded(child: _RoundList(results: results)),
            const Divider(height: 1, color: Colors.white12),
            _Footer(bridge: br),
          ],
        ),
      ),
    );
  }
}

class _Header extends StatelessWidget {
  const _Header({required this.results, required this.summary});

  final List<GameResult> results;
  final SessionSummary? summary;

  @override
  Widget build(BuildContext context) {
    final completed = results.length;
    // Total rounds: prefer summary, then last result's totalRounds, else
    // fall back to the count we have so the divisor never goes to zero.
    final total = summary?.totalRounds
        ?? (results.isNotEmpty ? results.last.totalRounds : 0);

    final isComplete = summary != null;

    return Padding(
      padding: const EdgeInsets.fromLTRB(14, 12, 14, 10),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            '오늘의 결과',
            style: TextStyle(
              color: Colors.white,
              fontSize: 14,
              fontWeight: FontWeight.w600,
              letterSpacing: 0.4,
            ),
          ),
          const SizedBox(height: 8),
          if (isComplete)
            _SummaryRow(summary: summary!)
          else
            _ProgressRow(completed: completed, total: total),
        ],
      ),
    );
  }
}

class _ProgressRow extends StatelessWidget {
  const _ProgressRow({required this.completed, required this.total});

  final int completed;
  final int total;

  @override
  Widget build(BuildContext context) {
    final ratio = total > 0 ? completed / total : 0.0;
    return Row(
      children: [
        Expanded(
          child: ClipRRect(
            borderRadius: BorderRadius.circular(3),
            child: LinearProgressIndicator(
              value: ratio.clamp(0.0, 1.0),
              minHeight: 4,
              backgroundColor: Colors.white12,
              valueColor: const AlwaysStoppedAnimation<Color>(Color(0xFF7BC67B)),
            ),
          ),
        ),
        const SizedBox(width: 10),
        Text(
          total > 0 ? '$completed / $total' : '$completed',
          style: const TextStyle(color: Colors.white70, fontSize: 12),
        ),
      ],
    );
  }
}

class _SummaryRow extends StatelessWidget {
  const _SummaryRow({required this.summary});

  final SessionSummary summary;

  @override
  Widget build(BuildContext context) {
    final allPass = summary.failCount == 0 && summary.successCount > 0;
    return Row(
      children: [
        Icon(
          allPass ? Icons.emoji_events : Icons.flag_circle,
          color: allPass ? const Color(0xFFFFD66B) : Colors.white70,
          size: 18,
        ),
        const SizedBox(width: 8),
        Text(
          '세션 완료  ${summary.successCount}/${summary.totalRounds} 성공',
          style: const TextStyle(color: Colors.white, fontSize: 13),
        ),
      ],
    );
  }
}

class _RoundList extends StatelessWidget {
  const _RoundList({required this.results});

  final List<GameResult> results;

  @override
  Widget build(BuildContext context) {
    if (results.isEmpty) {
      return const Center(
        child: Padding(
          padding: EdgeInsets.symmetric(horizontal: 16),
          child: Text(
            '한국어로 지시를 입력하면\n결과가 여기에 쌓입니다.',
            textAlign: TextAlign.center,
            style: TextStyle(color: Colors.white38, fontSize: 12, height: 1.5),
          ),
        ),
      );
    }

    // Reverse iteration order — newest at top.
    return ListView.separated(
      padding: const EdgeInsets.symmetric(vertical: 8, horizontal: 10),
      itemCount: results.length,
      separatorBuilder: (_, __) => const SizedBox(height: 6),
      itemBuilder: (context, i) {
        final r = results[results.length - 1 - i];
        return _ResultCard(result: r);
      },
    );
  }
}

class _ResultCard extends StatelessWidget {
  const _ResultCard({required this.result});

  final GameResult result;

  @override
  Widget build(BuildContext context) {
    final accent = result.success
        ? const Color(0xFF7BC67B)
        : const Color(0xFFE07A6E);
    final title = result.orderTitle.isNotEmpty
        ? result.orderTitle
        : (result.orderId.isNotEmpty ? result.orderId : '주문 미상');

    return DecoratedBox(
      decoration: BoxDecoration(
        color: Colors.white.withValues(alpha: 0.04),
        borderRadius: BorderRadius.circular(8),
        border: Border(
          left: BorderSide(color: accent, width: 3),
        ),
      ),
      child: Padding(
        padding: const EdgeInsets.fromLTRB(10, 8, 10, 9),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisSize: MainAxisSize.min,
          children: [
            Row(
              children: [
                Icon(
                  result.success ? Icons.check_circle : Icons.cancel,
                  color: accent,
                  size: 14,
                ),
                const SizedBox(width: 6),
                Expanded(
                  child: Text(
                    title,
                    style: const TextStyle(
                      color: Colors.white,
                      fontSize: 13,
                      fontWeight: FontWeight.w600,
                    ),
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
                if (result.totalRounds > 0)
                  Text(
                    '${result.roundIndex + 1}/${result.totalRounds}',
                    style: const TextStyle(color: Colors.white38, fontSize: 11),
                  ),
              ],
            ),
            if (result.instruction.isNotEmpty) ...[
              const SizedBox(height: 4),
              Text(
                '"${result.instruction}"',
                style: const TextStyle(
                  color: Colors.white60,
                  fontSize: 11,
                  fontStyle: FontStyle.italic,
                ),
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
              ),
            ],
            if (result.reason.isNotEmpty) ...[
              const SizedBox(height: 4),
              Text(
                result.reason,
                style: TextStyle(
                  color: Colors.white.withValues(alpha: 0.82),
                  fontSize: 12,
                  height: 1.35,
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _Footer extends StatelessWidget {
  const _Footer({required this.bridge});

  final FlutterBridge bridge;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 6),
      child: Row(
        children: [
          Expanded(
            child: TextButton.icon(
              icon: const Icon(Icons.refresh, size: 14, color: Colors.white70),
              onPressed: () => bridge.sendResetRound(),
              style: TextButton.styleFrom(
                foregroundColor: Colors.white70,
                minimumSize: const Size.fromHeight(32),
                padding: const EdgeInsets.symmetric(horizontal: 6),
              ),
              label: const Text('라운드 리셋', style: TextStyle(fontSize: 11)),
            ),
          ),
          const SizedBox(width: 4),
          Expanded(
            child: TextButton.icon(
              icon: const Icon(Icons.restart_alt, size: 14, color: Colors.white70),
              onPressed: () => bridge.sendRestartSession(),
              style: TextButton.styleFrom(
                foregroundColor: Colors.white70,
                minimumSize: const Size.fromHeight(32),
                padding: const EdgeInsets.symmetric(horizontal: 6),
              ),
              label: const Text('세션 재시작', style: TextStyle(fontSize: 11)),
            ),
          ),
        ],
      ),
    );
  }
}
