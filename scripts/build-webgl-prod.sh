#!/usr/bin/env bash
# Production WebGL build with Brotli compression.
# Pairs with game/Assets/Editor/WebGLBuildScript.cs (BuildWebGLMainProd).
#
# Brotli requires HTTPS delivery — serving this build through
# scripts/serve-webgl-probe.py (plain HTTP) will break the browser
# decoder. Use Vercel or any HTTPS host instead. For local iteration,
# use scripts/build-webgl-main.sh (Gzip + decompression fallback).

set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT="$ROOT/game"
VERSION_FILE="$PROJECT/ProjectSettings/ProjectVersion.txt"

if [ ! -f "$VERSION_FILE" ]; then
    echo "Missing $VERSION_FILE — is the Unity project initialised?" >&2
    exit 1
fi

VERSION=$(awk -F': ' '/^m_EditorVersion:/ {print $2}' "$VERSION_FILE" | tr -d '\r\n')
UNITY_APP="/Applications/Unity/Hub/Editor/$VERSION/Unity.app/Contents/MacOS/Unity"

if [ ! -x "$UNITY_APP" ]; then
    echo "Unity editor not found at $UNITY_APP" >&2
    echo "Install Unity $VERSION via Unity Hub, or adjust UNITY_APP in this script." >&2
    exit 1
fi

LOG="$PROJECT/Build/webgl-main-prod-build.log"
mkdir -p "$(dirname "$LOG")"

echo "Building Main Kitchen (Brotli production) with Unity $VERSION — log: $LOG"

"$UNITY_APP" \
    -batchmode -quit -nographics \
    -projectPath "$PROJECT" \
    -buildTarget WebGL \
    -executeMethod DayOneChef.Editor.WebGLBuildScript.BuildWebGLMainProd \
    -logFile "$LOG"

echo "Build OK → $PROJECT/Build/webgl-main-prod"
echo "Deploy to an HTTPS host (e.g. Vercel). Do NOT serve over plain HTTP."
