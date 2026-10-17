#!/usr/bin/env bash
# Runs the match scene directly (no main menu), as a normal game window.
# Arguments go to the match, for example:
#   scripts/run-match.sh --players=3 --session=loopback --latency=80
# Values: --players=N (1 to 4), --seed=N, --session=local|synctest|loopback, --latency=MS, --jitter=MS, --loss=PERCENT.
# Without arguments, the match uses the exports of Match.tscn (2 players).
# Players use keyboard 1 (WASD, Space, J, K), keyboard 2 (arrows, keypad 0, 1, 2), then the connected controllers.
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
godot="$("$here/godot-path.sh")"
exec "$godot" --path "$here/../project" res://scenes/match/Match.tscn -- "$@"
