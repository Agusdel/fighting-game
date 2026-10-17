#!/usr/bin/env bash
# For tests with the Godot MCP: the open editor runs the match scene directly (no main menu), with match arguments.
# A game started by the editor accepts MCP runtime commands; a game started from the shell does not.
#   scripts/mcp-play-match.sh --players=3 --session=loopback --latency=80
# The editor passes the project setting editor/run/main_run_args to the game. This script sets it, starts the game,
# and sets it back to empty, so project.godot does not change.
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
rpc() { node "$here/godot-rpc.mjs" "$@" > /dev/null; }
cli="$here/../mcp/godot-mcp-pro-v1.17.1/server/build/cli.js"

args="$*"
json_args=$(node -e 'process.stdout.write(JSON.stringify(process.argv[1]))' -- "${args:+-- $args}")
rpc set_project_setting "{\"key\":\"editor/run/main_run_args\",\"value\":$json_args,\"type\":\"string\"}"
trap 'rpc set_project_setting "{\"key\":\"editor/run/main_run_args\",\"value\":\"\",\"type\":\"string\"}"' EXIT
node "$cli" scene play --mode res://scenes/match/Match.tscn > /dev/null
sleep 2
echo "Match started: ${args:-(scene exports)}"
