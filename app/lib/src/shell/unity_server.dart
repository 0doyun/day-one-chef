// Thin `shelf_static` wrapper that serves the extracted Unity WebGL
// build on an ephemeral loopback port. Unity's `.unityweb` payloads
// come Gzip-compressed; we set `Content-Encoding: gzip` based on the
// file extension so the WebView's decompressor accepts them without
// the decompressionFallback path.

import 'dart:io';

import 'package:shelf/shelf.dart';
import 'package:shelf/shelf_io.dart' as shelf_io;
import 'package:shelf_static/shelf_static.dart';

class UnityServer {
  UnityServer._(this._server, this.url);

  final HttpServer _server;
  final Uri url;

  static Future<UnityServer> start(Directory rootDir) async {
    final handler = const Pipeline()
        .addMiddleware(_unityContentEncodingMiddleware)
        .addHandler(
          createStaticHandler(
            rootDir.path,
            defaultDocument: 'index.html',
            serveFilesOutsidePath: false,
          ),
        );

    final server = await shelf_io.serve(handler, InternetAddress.loopbackIPv4, 0);
    final url = Uri(
      scheme: 'http',
      host: server.address.address,
      port: server.port,
    );
    return UnityServer._(server, url);
  }

  Future<void> stop() async => _server.close(force: true);
}

/// Attaches `Content-Encoding` and explicit MIME for Unity's
/// `*.unityweb` artifacts so the browser decompresses and executes
/// them correctly. shelf_static alone uses `mime` lookup which does
/// not know about `.unityweb`.
Handler _unityContentEncodingMiddleware(Handler inner) {
  return (Request request) async {
    final response = await inner(request);
    final path = request.url.path;

    final lower = path.toLowerCase();
    if (!lower.endsWith('.unityweb')) return response;

    final headers = <String, Object>{
      ...response.headers,
      'content-encoding': 'gzip',
    };

    // The inner `.unityweb` is one of framework.js / wasm / data.
    // Peek at the path to set the right MIME type.
    if (lower.endsWith('.framework.js.unityweb')) {
      headers['content-type'] = 'application/javascript';
    } else if (lower.endsWith('.wasm.unityweb')) {
      headers['content-type'] = 'application/wasm';
    } else if (lower.endsWith('.data.unityweb')) {
      headers['content-type'] = 'application/octet-stream';
    }

    return response.change(headers: headers);
  };
}
