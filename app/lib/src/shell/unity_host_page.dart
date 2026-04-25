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

import '../game/providers.dart';
import 'flutter_bridge.dart';
import 'punchline_flash.dart';
import 'session_end_dialog.dart';
import 'side_panel.dart';
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
    // Pop the end-of-day modal the moment the evaluator's summary
    // lands. The result-log header alone changing to "세션 완료 N/5"
    // wasn't enough of a signal — players were left wondering whether
    // the game had frozen on the last round.
    ref.listen<SessionSummary?>(sessionSummaryProvider, (prev, next) {
      if (prev == null && next != null) {
        final br = _bridge;
        if (br != null) SessionEndDialog.show(context, br);
      }
    });
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
    return Column(
      children: [
        Expanded(
          child: Row(
            children: [
              Expanded(
                child: Stack(
                  children: [
                    WebViewWidget(controller: controller),
                    // Punchline flash overlay — shows the evaluator
                    // verdict big-and-centered for ~1.9s when a new
                    // round_end lands, then collapses into the side
                    // log. IgnorePointer inside, so it never steals
                    // taps from the canvas.
                    const Positioned.fill(child: PunchlineFlash()),
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
              SidePanel(bridge: _bridge),
            ],
          ),
        ),
        InstructionInputBar(bridge: _bridge),
      ],
    );
  }
}

class InstructionInputBar extends StatefulWidget {
  const InstructionInputBar({super.key, required this.bridge});

  final FlutterBridge? bridge;

  @override
  State<InstructionInputBar> createState() => _InstructionInputBarState();
}

class _InstructionInputBarState extends State<InstructionInputBar> {
  final TextEditingController _controller = TextEditingController();
  final FocusNode _focus = FocusNode();
  bool _sending = false;

  @override
  void dispose() {
    _controller.dispose();
    _focus.dispose();
    super.dispose();
  }

  Future<void> _send() async {
    final br = widget.bridge;
    final text = _controller.text.trim();
    if (br == null || text.isEmpty || _sending) return;
    setState(() => _sending = true);
    try {
      // Korean composition lives in Flutter's native IME — by the time
      // .text is read here the string is already composed.
      await br.sendSubmitInstruction(text);
      _controller.clear();
      _focus.requestFocus();
    } finally {
      if (mounted) setState(() => _sending = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Container(
      color: const Color(0xFF1A1A1F),
      padding: const EdgeInsets.fromLTRB(16, 10, 16, 14),
      child: Row(
        children: [
          Expanded(
            child: TextField(
              controller: _controller,
              focusNode: _focus,
              enabled: widget.bridge != null && !_sending,
              onSubmitted: (_) => _send(),
              maxLength: 80,
              maxLines: 1,
              style: const TextStyle(color: Colors.white, fontSize: 15),
              decoration: InputDecoration(
                isDense: true,
                counterText: '',
                hintText: '한국어로 지시 입력 후 Enter…',
                hintStyle: const TextStyle(color: Colors.white38),
                filled: true,
                fillColor: const Color(0xFF26262C),
                contentPadding:
                    const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(8),
                  borderSide: BorderSide.none,
                ),
              ),
            ),
          ),
          const SizedBox(width: 10),
          ElevatedButton(
            onPressed: widget.bridge != null && !_sending ? _send : null,
            style: ElevatedButton.styleFrom(
              backgroundColor: const Color(0xFFF2C84B),
              foregroundColor: const Color(0xFF1F140A),
              padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 14),
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(8),
              ),
            ),
            child: _sending
                ? const SizedBox(
                    width: 16, height: 16,
                    child: CircularProgressIndicator(strokeWidth: 2, color: Color(0xFF1F140A)),
                  )
                : const Text('지시 전송', style: TextStyle(fontWeight: FontWeight.w600)),
          ),
        ],
      ),
    );
  }
}
