// Day One Chef — Flutter entry point.
// See design/gdd/game-concept.md §5 (tech stack), §6 (bridge).

import 'package:flutter/material.dart';
import 'package:flutter_dotenv/flutter_dotenv.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'src/app.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  // `.env` is gitignored (see `.gitignore`). Missing file is tolerated
  // so `flutter test` doesn't require a key — the Gemini proxy returns
  // HTTP 500 with a human-readable error if the key isn't there when a
  // request actually arrives.
  try {
    await dotenv.load();
  } on Exception {
    // ignored — proxy handles the missing-key case at request time
  }
  runApp(const ProviderScope(child: DayOneChefApp()));
}
