# CLAUDE.md

## Project

This is a fighting game made with Godot 4 and C# (.NET).

## Repository layout

The repository root is NOT the Godot project root.

- `project/` — The Godot project. It contains `project.godot`, `FightingGame.csproj`, and `FightingGame.sln`. In Godot, `res://` points to this folder.
- Repository root and other subfolders — Scripts and tools that are not part of the Godot project (for example: build scripts, asset pipelines, editor utilities). Do not put these files in `project/`, and do not put game files outside `project/`.

Run Godot and `dotnet` commands for the game from `project/`, not from the repository root.

## Code

- Write all code in C#, unless the user specifies a different language.
- Do not use GDScript unless the user asks for it.
- Do not reference `project.md` (or any other design document) in code or comments. The design document can be removed in the future. If code needs an explanation, write the full explanation in a comment in the code file.

## Communication

Write all communication in ASD-STE100 Simplified Technical English:

- Use short sentences (maximum 20 words for procedures, 25 words for descriptions).
- Write one instruction in each sentence.
- Use the active voice.
- Use the imperative form for instructions.
- Use simple words with one meaning. Use the same word for the same thing each time.
- Use technical names (class names, file names, Godot terms) without change.

## Godot files

Godot links files with UIDs (`.uid` sidecar files for scripts, a `uid` in the header of `.tscn`/`.tres`, `.import` files for assets). A broken link corrupts scenes or loses references.

- Do not create, edit, move, or delete `.tscn`, `.tres`, or imported assets with text tools. Ask the user to do it in the editor.
- You can create new `.cs` files. Do not create the `.uid` file. Godot creates it during the build.
- To move or rename a `.cs` file, move its `.uid` file with it (`git mv` both).
- To delete a `.cs` file, delete its `.uid` file too.
- Files in `project/simulation/` are not imported by Godot (`.gdignore`). These rules do not apply there.

## Build and test

Use the scripts in `scripts/` (run them from any folder):

- `scripts/build.sh` — Runs `dotnet build` on `FightingGame.sln`, then a Godot headless build (the same as the editor Build button). Use `--no-godot` to skip the Godot build.
- `scripts/test.sh` — Runs all unit tests. Extra arguments go to `dotnet test` (for example `--filter FixedTests`).
- `scripts/godot-path.sh` — Prints the Godot executable path. It reads `$GODOT_BIN`, then `godotTools.editorPath.godot4` in `fighting-game.code-workspace`.

Run `scripts/build.sh` and `scripts/test.sh` after each code change.

Only the .NET 10 runtime is installed. The test project targets `net8.0` and uses `RollForward=Major` to run on .NET 10.

## Design document

- Follow the design in `project.md` (repository root). It is the source of truth for the architecture and the milestones.
- When a design decision changes or is added, update `project.md` (and its Decision Log) in the same change.

## Determinism rules

- The simulation code is in the class library `project/simulation/FightingGame.Simulation.csproj`. It must not reference Godot. Do not add Godot packages to it.
- Put Godot code (views, UI, authoring nodes, Godot transports) in `project/src/`.
- Put tests in `tests/` and tools in `tools/` (outside `project/`).
- Do not use `float`, `double`, `System.Random`, `DateTime`, or static mutable state in the simulation.
- Do not iterate `Dictionary` or `HashSet` in the simulation.
- Use the fixed-point types (`Fixed`, `FixedVector2`, `FixedAABB`) for all simulation values.
- Keep the world state as plain value types, so it can be saved, restored, and hashed.
