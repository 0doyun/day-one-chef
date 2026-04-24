#!/usr/bin/env python3
# Static server for the Phase 1 probe build.
# Unity's default WebGL build emits .br (Brotli) files; Python's built-in
# http.server doesn't know to send `Content-Encoding: br`, so browsers see
# a raw compressed blob and bail out with SyntaxError. This handler sets the
# encoding + MIME type based on the extension so the browser decodes the
# response as expected.
#
# Usage: ./scripts/serve-webgl-probe.py [port]
# Default port: 8080. Ctrl+C to stop.

import http.server
import os
import socketserver
import sys
from pathlib import Path


ENCODING_BY_SUFFIX = {
    ".br": "br",
    ".gz": "gzip",
}

MIME_BY_INNER_SUFFIX = {
    ".js": "application/javascript",
    ".wasm": "application/wasm",
    ".data": "application/octet-stream",
    ".symbols.json": "application/json",
}


class WebGLBuildHandler(http.server.SimpleHTTPRequestHandler):
    def end_headers(self):
        suffix = Path(self.path).suffix
        if suffix in ENCODING_BY_SUFFIX:
            self.send_header("Content-Encoding", ENCODING_BY_SUFFIX[suffix])
        super().end_headers()

    def guess_type(self, path):
        stem = Path(path)
        if stem.suffix in ENCODING_BY_SUFFIX:
            inner = stem.with_suffix("").suffix
            if inner in MIME_BY_INNER_SUFFIX:
                return MIME_BY_INNER_SUFFIX[inner]
        return super().guess_type(path)


def main() -> None:
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 8080
    build_dir = Path(__file__).resolve().parent.parent / "game" / "Build" / "webgl-ime-probe"
    if not build_dir.is_dir():
        sys.exit(f"Build directory not found: {build_dir}\n"
                 "Run ./scripts/build-webgl-probe.sh first.")
    os.chdir(build_dir)

    with socketserver.TCPServer(("", port), WebGLBuildHandler) as httpd:
        print(f"Serving {build_dir} at http://localhost:{port}  (Ctrl+C to stop)")
        httpd.serve_forever()


if __name__ == "__main__":
    main()
