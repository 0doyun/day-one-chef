// Extracts the bundled Unity WebGL build from Flutter's asset bundle
// out to a real directory on disk. `shelf_static` needs a directory to
// serve from, so we copy the assets listed in AssetManifest.json the
// first time the app runs (idempotent — subsequent launches reuse the
// cached copy).

import 'dart:convert';
import 'dart:io';

import 'package:flutter/services.dart' show rootBundle;
import 'package:path/path.dart' as p;
import 'package:path_provider/path_provider.dart';

class UnityAssetsExtractor {
  static const String _manifestKey = 'AssetManifest.json';
  static const String _assetPrefix = 'assets/unity/';

  /// Extracts every `assets/unity/**` entry from the asset bundle into
  /// the application's support directory and returns the root of the
  /// extracted tree.
  ///
  /// Cache strategy: byte-level compare. We always have to load the
  /// asset bytes from rootBundle to know the new content anyway, so a
  /// length-only check (the previous heuristic) is an unsafe optimisation
  /// — Day 13 chef-animator builds shipped the same 10.5 MB wasm size
  /// after code changes, and the size-stable cache silently kept the
  /// old build. Always comparing bytes keeps reads cheap (rootBundle
  /// uses a memory-mapped view in release) and only pays the disk write
  /// cost when content actually changed.
  static Future<Directory> extract() async {
    final support = await getApplicationSupportDirectory();
    final target = Directory(p.join(support.path, 'unity_web'));
    if (!target.existsSync()) target.createSync(recursive: true);

    final manifestJson = await rootBundle.loadString(_manifestKey);
    final Map<String, dynamic> manifest = json.decode(manifestJson) as Map<String, dynamic>;

    for (final assetKey in manifest.keys) {
      if (!assetKey.startsWith(_assetPrefix)) continue;

      final relative = assetKey.substring(_assetPrefix.length);
      if (relative.isEmpty) continue;

      final outFile = File(p.join(target.path, relative));
      outFile.parent.createSync(recursive: true);

      final data = await rootBundle.load(assetKey);
      final bytes = data.buffer.asUint8List();

      if (outFile.existsSync() &&
          outFile.lengthSync() == bytes.length &&
          _bytesEqual(outFile.readAsBytesSync(), bytes)) {
        continue;
      }

      await outFile.writeAsBytes(bytes, flush: true);
    }

    return target;
  }

  static bool _bytesEqual(List<int> a, List<int> b) {
    if (a.length != b.length) return false;
    for (var i = 0; i < a.length; i++) {
      if (a[i] != b[i]) return false;
    }
    return true;
  }
}
