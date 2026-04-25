// Server-side Gemini proxy. Unity's WebGL build POSTs to
//   /api/gemini/<model>:generateContent
// on this same-origin shelf server; this handler forwards the body
// up to Google with the `x-goog-api-key` header taken from the
// dotenv-loaded `GEMINI_API_KEY`. The Unity bundle therefore ships
// without a key — see ADR-0003 Phase B.

import 'dart:convert';

import 'package:flutter/foundation.dart' show debugPrint;
import 'package:flutter_dotenv/flutter_dotenv.dart';
import 'package:http/http.dart' as http;
import 'package:shelf/shelf.dart';
import 'package:shelf_router/shelf_router.dart';

class GeminiProxy {
  static const String _googleEndpoint =
      'https://generativelanguage.googleapis.com/v1beta/models';

  Router buildRouter() {
    final router = Router()
      ..post('/api/gemini/<modelAndAction|.+>', _handleGenerate);
    return router;
  }

  Future<Response> _handleGenerate(Request request, String modelAndAction) async {
    final apiKey = dotenv.maybeGet('GEMINI_API_KEY') ?? '';
    if (apiKey.isEmpty) {
      return Response(
        500,
        body: json.encode({
          'error': 'GEMINI_API_KEY not set in app/.env — copy '
              'app/.env.example and fill in a key before running the shell.',
        }),
        headers: {'content-type': 'application/json'},
      );
    }

    final body = await request.readAsString();
    final url = Uri.parse('$_googleEndpoint/$modelAndAction');
    debugPrint('[GeminiProxy] → POST $url  bytes=${body.length}');

    try {
      final upstream = await http
          .post(
            url,
            headers: {
              'content-type': 'application/json',
              'x-goog-api-key': apiKey,
            },
            body: body,
          )
          .timeout(const Duration(seconds: 10));

      debugPrint('[GeminiProxy] ← HTTP ${upstream.statusCode} '
          '(${upstream.bodyBytes.length} bytes)');

      return Response(
        upstream.statusCode,
        body: upstream.bodyBytes,
        headers: {
          'content-type': upstream.headers['content-type'] ?? 'application/json',
        },
      );
    } on Exception catch (e) {
      debugPrint('[GeminiProxy] upstream error: $e');
      return Response(
        502,
        body: json.encode({'error': 'Upstream Gemini call failed: $e'}),
        headers: {'content-type': 'application/json'},
      );
    }
  }
}
