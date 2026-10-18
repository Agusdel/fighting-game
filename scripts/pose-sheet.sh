#!/usr/bin/env bash
# Renders frames of fighter animations side by side into one PNG, then quits (a short game window opens).
# Each cell: the figure at one frame, its label (yellow = a key frame), and the active hurtboxes and hitboxes.
#   scripts/pose-sheet.sh --anims=Idle,Walk --out=/tmp/sheet.png
#   scripts/pose-sheet.sh --sequence=Jump:10-21,Fall,Land,Idle:0-3 --out=/tmp/sequence.png
#   scripts/pose-sheet.sh "--sequence=Idle:0,PlatformDrop,Fall;Walk:0,Fall"   (";" = a new row; quote it in the shell)
# Arguments: --fighter=Fighter01 (or a res:// path), --anims=A,B (default: all), --sequence=Name:from-to,...,
#   --out=PATH (default: /tmp/fighting-game/pose_sheet.png, in the system temp folder), --scale=2, --columns=16,
#   --boxes=0 (no boxes).
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
godot="$("$here/godot-path.sh")"
"$godot" --path "$here/../project" res://scenes/debug/PoseSheet.tscn -- "$@" 2>&1 | grep -E "PoseSheet|ERROR|Exception" || true
