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

## Communication

Write all communication in ASD-STE100 Simplified Technical English:

- Use short sentences (maximum 20 words for procedures, 25 words for descriptions).
- Write one instruction in each sentence.
- Use the active voice.
- Use the imperative form for instructions.
- Use simple words with one meaning. Use the same word for the same thing each time.
- Use technical names (class names, file names, Godot terms) without change.

## Build and test

Run these commands from `project/`:

- Build all projects: `dotnet build FightingGame.sln`
- Run the tests: `dotnet test FightingGame.sln`

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
- Use the fixed-point types (`Fixed`, `FixedVector2`, `FixedAabb`) for all simulation values.
- Keep the world state as plain value types, so it can be saved, restored, and hashed.
