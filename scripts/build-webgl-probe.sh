#!/usr/bin/env bash
# Headless WebGL build for the OSS_IME_Probe scene.
# Pairs with game/Assets/Editor/WebGLBuildScript.cs.
#
# Unity editor path is derived from game/ProjectSettings/ProjectVersion.txt
# so the script always matches the pinned engine version.

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

LOG="$PROJECT/Build/webgl-build.log"
mkdir -p "$(dirname "$LOG")"

echo "Building WebGL with Unity $VERSION — log: $LOG"

"$UNITY_APP" \
    -batchmode -quit -nographics \
    -projectPath "$PROJECT" \
    -buildTarget WebGL \
    -executeMethod DayOneChef.Editor.WebGLBuildScript.BuildWebGL \
    -logFile "$LOG"

echo "Build OK → $PROJECT/Build/webgl-ime-probe"
echo "Serve locally: (cd $PROJECT/Build/webgl-ime-probe && python3 -m http.server 8080)"
