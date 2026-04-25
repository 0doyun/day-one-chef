// Hosts the Unity WebGL build inside a WebView. Lifecycle:
//   1. Extract bundled Unity build → app support dir (cached).
//   2. Start a loopback shelf server rooted at that dir.
//   3. Point the WebView at http://127.0.0.1:<port>/index.html.
//
// Bidirectional bridge (Unity ↔ JS ↔ Flutter) lands in Day 9 via a
// JavaScriptChannel registered on this page.

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:webview_flutter/webview_flutter.dart';

import 'flutter_bridge.dart';
import 'result_log_panel.dart';
import 'unity_assets.dart';
import 'unity_server.dart';

class UnityHostPage extends ConsumerStatefulWidget {
  const UnityHostPage({super.key});

  @override
  ConsumerState<UnityHostPage> createState() => _UnityHostPageState();
}

class _UnityHostPageState extends ConsumerState<UnityHostPage> {
  UnityServer? _server;
  WebViewController? _controller;
  FlutterBridge? _bridge;
  String? _error;
  int _loadPercent = 0;

  @override
  void initState() {
    super.initState();
    _bootstrap();
  }

  @override
  void dispose() {
    _server?.stop();
    super.dispose();
  }

  Future<void> _bootstrap() async {
    try {
      final root = await UnityAssetsExtractor.extract();
      final server = await UnityServer.start(root);
      if (!mounted) {
        await server.stop();
        return;
      }
      final controller = WebViewController()
        ..setJavaScriptMode(JavaScriptMode.unrestricted)
        // setBackgroundColor is unimplemented on macOS WKWebView; the
        // Unity template (Assets/WebGLTemplates/DayOneChef/index.html)
        // already paints the canvas #1A1A1A so the flash-of-white on
        // first paint is bounded. Try it on platforms that support it,
        // swallow the UnimplementedError otherwise.
        ..setNavigationDelegate(
          NavigationDelegate(
            onProgress: (p) {
              if (mounted) setState(() => _loadPercent = p);
            },
            onWebResourceError: (err) {
              if (mounted) setState(() => _error = err.description);
            },
          ),
        );
      final bridge = FlutterBridge(ref: ref, controller: controller)..attach();
      try {
        await controller.setBackgroundColor(const Color(0xFF1A1A1A));
      } on UnimplementedError {
        // macOS WKWebView backend — harmless, template paints black.
      }
      await controller.loadRequest(server.url.resolve('index.html'));

      setState(() {
        _server = server;
        _controller = controller;
        _bridge = bridge;
      });
    } catch (e, st) {
      // Widen the catch to Object — errors like `UnimplementedError`
      // (platform-unsupported WebView methods on macOS) are Error
      // subclasses, not Exception subclasses, and slip past
      // `on Exception`. Log the stack so debug output names the
      // actual plugin method that blew up.
      debugPrint('[UnityHostPage] bootstrap failed: $e\n$st');
      if (mounted) setState(() => _error = '$e');
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFF1A1A1A),
      body: SafeArea(child: _body()),
    );
  }

  Widget _body() {
    if (_error != null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Text(
            'Unity 로드 실패: $_error',
            style: const TextStyle(color: Colors.white70),
            textAlign: TextAlign.center,
          ),
        ),
      );
    }
    final controller = _controller;
    if (controller == null) {
      return const Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            CircularProgressIndicator(),
            SizedBox(height: 16),
            Text('주방 준비 중…', style: TextStyle(color: Colors.white70)),
          ],
        ),
      );
    }
    return Row(
      children: [
        Expanded(
          child: Stack(
            children: [
              WebViewWidget(controller: controller),
              if (_loadPercent < 100)
                Positioned(
                  left: 0, right: 0, bottom: 0,
                  child: LinearProgressIndicator(
                    value: _loadPercent / 100,
                    minHeight: 2,
                    backgroundColor: Colors.transparent,
                  ),
                ),
            ],
          ),
        ),
        ResultLogPanel(bridge: _bridge),
      ],
    );
  }
}
