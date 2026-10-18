#!/usr/bin/env bash
# Generates fighter animations from a pose table with the open Godot editor (MCP), then saves Fighter01.tscn.
#   tools/pose-generator/generate.sh tools/pose-generator/fighter01-poses.txt
# The table can hold all animations or only the ones to change (only these are replaced). See fighter01-poses.txt
# for the format and gen.gd for what the script changes. Check the result with scripts/pose-sheet.sh.
# Do not run it while Fighter01.tscn has unsaved changes in the editor that you want to keep: it saves the scene.
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$here/../.."
cli="node $root/mcp/godot-mcp-pro-v1.17.1/server/build/cli.js"
table="$(grep -v '^#' "$1")"
script="$(python3 -c 'import sys; print(open(sys.argv[1]).read().replace("__POSES__", sys.argv[2]))' "$here/gen.gd" "$table")"

$cli scene open --path res://scenes/fighters/Fighter01.tscn > /dev/null 2>&1
current=""
for _ in 1 2 3 4 5; do
  current=$($cli scene tree 2>&1 | grep -o '"scene_path": "[^"]*"' || true)
  [ "$current" = '"scene_path": "res://scenes/fighters/Fighter01.tscn"' ] && break
  sleep 2
done
if [ "$current" != '"scene_path": "res://scenes/fighters/Fighter01.tscn"' ]; then
  echo "Fighter01.tscn is not the open scene ($current). Nothing changed." >&2
  exit 1
fi
$cli editor exec --code "$script" 2>&1 | sed -n '/output/,/\]/p;/rror/p' | tr '\\' '\n' | sed 's/^n//'
$cli scene save 2>&1 | grep -iE "error|saved"
