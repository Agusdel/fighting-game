#!/usr/bin/env bash
# Runs all unit tests. Extra arguments go to "dotnet test" (for example: --filter FixedTests).
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT/project"
dotnet test FightingGame.sln --nologo "$@"
