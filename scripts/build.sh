#!/usr/bin/env bash
# Builds all projects.
#   1. dotnet build of FightingGame.sln (shows compiler errors).
#   2. Godot headless build (the same build as the editor Build button).
# Usage: scripts/build.sh [--no-godot]
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT/project"

echo "== dotnet build"
dotnet build FightingGame.sln --nologo -v quiet -clp:NoSummary

if [[ "${1:-}" == "--no-godot" ]]; then
    exit 0
fi

echo "== Godot build"
GODOT="$("$ROOT/scripts/godot-path.sh")"
LOG="$(mktemp)"
trap 'rm -f "$LOG"' EXIT
if ! "$GODOT" --headless --path . --build-solutions --quit >"$LOG" 2>&1; then
    grep -E "ERROR|error" "$LOG" >&2 || tail -20 "$LOG" >&2
    echo "Godot build FAILED" >&2
    exit 1
fi
echo "Build OK"
