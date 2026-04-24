// Day One Chef — Flutter entry point.
// See design/gdd/game-concept.md §5 (tech stack), §6 (bridge).

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'src/app.dart';

void main() {
  runApp(const ProviderScope(child: DayOneChefApp()));
}
