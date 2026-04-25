// Right-edge sidebar that pairs the active order ticket (RecipePanel)
// with the round-result history (ResultLogPanel). Lives at a single
// fixed width so the Unity canvas keeps a stable aspect ratio across
// the session.

import 'package:flutter/material.dart';

import 'flutter_bridge.dart';
import 'recipe_panel.dart';
import 'result_log_panel.dart';

class SidePanel extends StatelessWidget {
  const SidePanel({super.key, required this.bridge});

  final FlutterBridge? bridge;

  static const double width = 300;

  @override
  Widget build(BuildContext context) {
    if (bridge == null) return const SizedBox(width: width);
    return SizedBox(
      width: width,
      child: Padding(
        padding: const EdgeInsets.fromLTRB(8, 10, 10, 10),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const RecipePanel(),
            const SizedBox(height: 10),
            Expanded(child: ResultLogPanel(bridge: bridge)),
          ],
        ),
      ),
    );
  }
}
