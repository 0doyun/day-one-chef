#!/usr/bin/env bash
# Sync the Unity WebGL dev build into the Flutter shell's asset tree.
#
# Usage:   ./scripts/sync-unity-to-app.sh
# Outputs: app/assets/unity/{index.html, Build/*}
#
# Uses the Gzip + decompressionFallback dev build so it works inside
# the Flutter WebView regardless of Content-Encoding handling. Prod
# builds (Brotli, scripts/build-webgl-prod.sh) are only valid over
# HTTPS and don't belong in the Flutter shell's asset bundle.

set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SRC="$ROOT/game/Build/webgl-main"
DEST="$ROOT/app/assets/unity"

if [ ! -d "$SRC" ]; then
    echo "Unity build not found at $SRC." >&2
    echo "Run ./scripts/build-webgl-main.sh first." >&2
    exit 1
fi

mkdir -p "$DEST"
rm -rf "$DEST/Build" "$DEST/TemplateData" "$DEST/index.html"
cp -R "$SRC/"* "$DEST/"

echo "Synced Unity build → $DEST"
du -sh "$DEST" 2>/dev/null
