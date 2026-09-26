#!/usr/bin/env bash
# Prints the path of the Godot executable.
# Order: $GODOT_BIN, then "godotTools.editorPath.godot4" in fighting-game.code-workspace.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if [[ -n "${GODOT_BIN:-}" ]]; then
    echo "$GODOT_BIN"
    exit 0
fi

WORKSPACE="$ROOT/fighting-game.code-workspace"
GODOT="$(grep -o '"godotTools.editorPath.godot4"[^,}]*' "$WORKSPACE" 2>/dev/null | sed -E 's/.*:[[:space:]]*"([^"]*)".*/\1/' || true)"

if [[ -z "$GODOT" || ! -x "$GODOT" ]]; then
    echo "error: Godot executable not found. Set GODOT_BIN or godotTools.editorPath.godot4 in $WORKSPACE." >&2
    exit 1
fi
echo "$GODOT"
