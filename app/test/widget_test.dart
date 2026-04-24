// Placeholder widget test. Replaced with real shell tests in Day 9+
// once the bridge is wired.
//
// Note: `UnityHostPage` requires `path_provider` + `shelf` at runtime,
// which aren't usable in the default test environment. Real tests
// will mock those via a `UnityHostDeps`-style seam.

import 'package:flutter_test/flutter_test.dart';

void main() {
  test('shell placeholder', () {
    expect(1 + 1, equals(2));
  });
}
