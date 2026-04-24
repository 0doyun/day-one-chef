#!/usr/bin/env bash
# Headless WebGL build for the Day 3 prototype MainKitchen scene.
# Pairs with game/Assets/Editor/WebGLBuildScript.cs (BuildWebGLMain).
#
# Generates Assets/Scenes/MainKitchen.unity on first run and any time
# it is missing (the scene is a derived artifact, not committed).

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

LOG="$PROJECT/Build/webgl-main-build.log"
mkdir -p "$(dirname "$LOG")"

echo "Building Main Kitchen (Day 3 prototype) with Unity $VERSION — log: $LOG"

"$UNITY_APP" \
    -batchmode -quit -nographics \
    -projectPath "$PROJECT" \
    -buildTarget WebGL \
    -executeMethod DayOneChef.Editor.WebGLBuildScript.BuildWebGLMain \
    -logFile "$LOG"

echo "Build OK → $PROJECT/Build/webgl-main"
echo "Serve locally: ./scripts/serve-webgl-main.py"
