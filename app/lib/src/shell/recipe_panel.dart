// Right-edge order ticket — replaces the old in-Unity OrderCard. Reads
// the active order from `currentOrderProvider` and renders it as a
// vertical list of ingredient chips with sprite icons. Sprite assets
// live under app/assets/icons/Ing_*.png; the path map below mirrors the
// Unity IngredientType enum names emitted in the bridge payload.

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../game/bridge_message.dart' show OrderComponent;
import '../game/providers.dart';

class RecipePanel extends ConsumerWidget {
  const RecipePanel({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final order = ref.watch(currentOrderProvider);
    return DecoratedBox(
      decoration: BoxDecoration(
        color: const Color(0xFF1F1612).withValues(alpha: 0.92),
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: Colors.white.withValues(alpha: 0.06)),
      ),
      child: Padding(
        padding: const EdgeInsets.fromLTRB(14, 14, 14, 14),
        child: order == null ? const _EmptyOrder() : _OrderTicket(order: order),
      ),
    );
  }
}

class _EmptyOrder extends StatelessWidget {
  const _EmptyOrder();

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Row(
          children: [
            Icon(Icons.restaurant_menu,
                size: 16, color: Colors.white.withValues(alpha: 0.6)),
            const SizedBox(width: 6),
            const Text('주문서',
                style: TextStyle(
                  color: Colors.white70,
                  fontSize: 13,
                  fontWeight: FontWeight.w600,
                  letterSpacing: 0.4,
                )),
          ],
        ),
        const SizedBox(height: 12),
        const Text(
          '주방을 준비하는 중…',
          style: TextStyle(color: Colors.white38, fontSize: 12, height: 1.5),
        ),
      ],
    );
  }
}

class _OrderTicket extends StatelessWidget {
  const _OrderTicket({required this.order});

  final CurrentOrder order;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Row(
          children: [
            const Icon(Icons.receipt_long,
                size: 16, color: Color(0xFFF2C84B)),
            const SizedBox(width: 6),
            const Text('주문서',
                style: TextStyle(
                  color: Color(0xFFF2C84B),
                  fontSize: 13,
                  fontWeight: FontWeight.w600,
                  letterSpacing: 0.4,
                )),
            const Spacer(),
            if (order.totalRounds > 0)
              Text(
                '${order.roundIndex + 1} / ${order.totalRounds}',
                style: const TextStyle(
                  color: Colors.white60,
                  fontSize: 11,
                  fontWeight: FontWeight.w500,
                ),
              ),
          ],
        ),
        const SizedBox(height: 10),
        Text(
          order.recipeName.isEmpty ? '주문 미상' : order.recipeName,
          style: const TextStyle(
            color: Colors.white,
            fontSize: 22,
            fontWeight: FontWeight.w700,
            height: 1.1,
          ),
        ),
        const SizedBox(height: 4),
        Container(
          height: 1,
          color: const Color(0xFFF2C84B).withValues(alpha: 0.2),
        ),
        const SizedBox(height: 10),
        const Text('재료',
            style: TextStyle(
              color: Colors.white54,
              fontSize: 11,
              letterSpacing: 1.2,
              fontWeight: FontWeight.w600,
            )),
        const SizedBox(height: 8),
        for (final c in order.components) _ComponentRow(c: c),
      ],
    );
  }
}

class _ComponentRow extends StatelessWidget {
  const _ComponentRow({required this.c});

  final OrderComponent c;

  static const Map<String, String> _iconAssets = {
    'Bread': 'assets/icons/Ing_Bread.png',
    'Patty': 'assets/icons/Ing_Patty.png',
    'Cheese': 'assets/icons/Ing_Cheese.png',
    'Lettuce': 'assets/icons/Ing_Lettuce.png',
    'Tomato': 'assets/icons/Ing_Tomato.png',
    'Egg': 'assets/icons/Ing_Egg.png',
  };

  @override
  Widget build(BuildContext context) {
    final asset = _iconAssets[c.type];
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Row(
        children: [
          Container(
            width: 36,
            height: 36,
            decoration: BoxDecoration(
              color: Colors.white.withValues(alpha: 0.05),
              borderRadius: BorderRadius.circular(6),
            ),
            alignment: Alignment.center,
            child: asset == null
                ? const Icon(Icons.help_outline,
                    color: Colors.white38, size: 18)
                : Image.asset(
                    asset,
                    width: 28,
                    height: 28,
                    filterQuality: FilterQuality.none,
                  ),
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(
                  c.typeKr.isEmpty ? c.type : c.typeKr,
                  style: const TextStyle(
                    color: Colors.white,
                    fontSize: 13,
                    fontWeight: FontWeight.w600,
                  ),
                ),
                const SizedBox(height: 1),
                Text(
                  c.stateKr.isEmpty ? c.state : c.stateKr,
                  style: TextStyle(
                    color: Colors.white.withValues(alpha: 0.55),
                    fontSize: 11,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
