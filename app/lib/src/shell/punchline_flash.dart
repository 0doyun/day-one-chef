// Centered transient overlay that pops the evaluator's verdict in
// large text the moment a round_end lands, then fades after ~1.8s.
// This is the creative-director P0 from the Day 13-B review: the
// reason is the punchline and was being buried in 12px sidebar text.
// Now it gets a held shot before settling into the result log.

import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../game/models/game_result.dart';
import '../game/providers.dart';

class PunchlineFlash extends ConsumerStatefulWidget {
  const PunchlineFlash({super.key});

  @override
  ConsumerState<PunchlineFlash> createState() => _PunchlineFlashState();
}

class _PunchlineFlashState extends ConsumerState<PunchlineFlash> {
  GameResult? _shown;
  int _seenLength = 0;
  Timer? _dismiss;

  @override
  void dispose() {
    _dismiss?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    ref.listen<List<GameResult>>(gameResultsProvider, (prev, next) {
      // Trigger only on append (length grew). Filtering on receivedAt
      // would also work but length is cheaper and doesn't trip on the
      // first frame's empty→empty rebuild.
      if (next.length > _seenLength) {
        _seenLength = next.length;
        final fresh = next.last;
        if (fresh.reason.isNotEmpty) {
          setState(() => _shown = fresh);
          _dismiss?.cancel();
          _dismiss = Timer(const Duration(milliseconds: 1900), () {
            if (mounted) setState(() => _shown = null);
          });
        }
      } else if (next.length < _seenLength) {
        // Session restart / round reset — drop any in-flight flash.
        _seenLength = next.length;
        _dismiss?.cancel();
        if (_shown != null) setState(() => _shown = null);
      }
    });

    return IgnorePointer(
      ignoring: true,
      child: AnimatedSwitcher(
        duration: const Duration(milliseconds: 280),
        switchInCurve: Curves.easeOutBack,
        switchOutCurve: Curves.easeIn,
        child: _shown == null
            ? const SizedBox.shrink(key: ValueKey('empty'))
            : _Card(key: ValueKey(_shown!.receivedAt), result: _shown!),
      ),
    );
  }
}

class _Card extends StatelessWidget {
  const _Card({super.key, required this.result});

  final GameResult result;

  @override
  Widget build(BuildContext context) {
    final accent = result.success
        ? const Color(0xFF7BC67B)
        : const Color(0xFFE07A6E);
    final headline = result.success ? '성공!' : '망함…';
    return Center(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 540),
        child: Container(
          margin: const EdgeInsets.symmetric(horizontal: 24),
          padding: const EdgeInsets.fromLTRB(22, 18, 22, 20),
          decoration: BoxDecoration(
            color: const Color(0xCC1E1612),
            borderRadius: BorderRadius.circular(14),
            border: Border.all(color: accent, width: 2),
            boxShadow: [
              BoxShadow(
                color: accent.withValues(alpha: 0.35),
                blurRadius: 24,
                spreadRadius: 1,
              ),
            ],
          ),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Icon(
                    result.success ? Icons.check_circle : Icons.cancel,
                    color: accent,
                    size: 22,
                  ),
                  const SizedBox(width: 8),
                  Text(
                    headline,
                    style: TextStyle(
                      color: accent,
                      fontSize: 22,
                      fontWeight: FontWeight.w700,
                      letterSpacing: 0.5,
                    ),
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Text(
                      result.orderTitle,
                      style: const TextStyle(
                        color: Colors.white60,
                        fontSize: 13,
                        fontStyle: FontStyle.italic,
                      ),
                      overflow: TextOverflow.ellipsis,
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 12),
              Text(
                result.reason,
                style: const TextStyle(
                  color: Colors.white,
                  fontSize: 18,
                  height: 1.4,
                  fontWeight: FontWeight.w500,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
