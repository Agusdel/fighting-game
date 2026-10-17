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
- Naming: simulation types that hold data use the `Data` suffix (`WorldData`, `FighterData`, `StageData`, `GameData`). Exceptions: `FighterStats`, `FrameInput`, and enums.
- Naming: use the term "spawn position" (`SpawnPosition...`), never only "spawn".
- Naming: do not name a namespace like a Godot class (for example `FightingGame.Input`). It hides the Godot class (`Input`) in all code under `FightingGame.*`.
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

- Do not create, edit, move, or delete `.tscn`, `.tres`, `project.godot`, or imported assets with text tools. Always use the Godot MCP tools (see "Godot MCP" below).
- If the Godot MCP is not connected, ask the user to open the editor. Do not use text tools as a fallback.
- You can create new `.cs` files. Do not create the `.uid` file. Godot creates it during the build.
- To move or rename a `.cs` file, move its `.uid` file with it (`git mv` both).
- To delete a `.cs` file, delete its `.uid` file too.
- Files in `project/simulation/` are not imported by Godot (`.gdignore`). These rules do not apply there.

## Godot MCP

The Godot MCP Pro server (`godot-mcp-pro`) connects Claude to the open Godot editor. `.mcp.json` registers it. The server is in `mcp/godot-mcp-pro-v1.17.1/server/`. The editor plugin is in `project/addons/godot_mcp/`.

- Use the MCP tools for all work on scenes, nodes, resources, project settings, and input actions.
- The Godot editor must be open with the project, and the plugin must be enabled.
- If the `mcp__godot-mcp-pro__*` tools are not loaded, use the CLI: `node mcp/godot-mcp-pro-v1.17.1/server/build/cli.js <group> <command>`. Run `--help` to see the commands.
- Write scripts in C#, with the usual file tools. Do not use `create_script` or `edit_script` to make GDScript files.
- `execute_editor_script` and `execute_game_script` take GDScript. You can use them for editor tasks. Do not save that code as project files.
- `create_scene` does not open the new scene. Call `open_scene` before `add_node`, or the nodes go into the scene that is open now.
- `get_scene_tree` shows the path of the open scene. Check it before you edit or save.
- `scene create --root_type` accepts only built-in Godot classes, not script classes (for example `FighterRoot`). If it fails, the scene is not created and not opened, and the next `node add` calls go into the scene that is open now. Create the scene with a built-in root type (for example `Node2D`), open it, check `scene_path` with `scene tree`, then attach the script to the root (`script attach --node_path .`). `node add` accepts script classes.
- The editor (4.8-dev6) closed with no error two times: after a change to `FighterBase.tscn` was saved, and then the open inherited scene `Fighter01.tscn` was shown again (Godot reloads it because its base changed). Before you change a base scene, close the tabs of the scenes that inherit it (`EditorInterface.close_scene()` while that scene is the current one), and open them again after the save.
- In an editor script, do not set bone rotations on the scene nodes to check a pose, or restore the exact old position, rotation, and scale after the check. Godot recalculates a `Bone2D` scale from its matrix on load, so the leg bones already show scale noise (0.9999998) that is saved as an override.
- `editor exec` refuses scripts that write files (for example `ResourceSaver.save`). Create and save scenes with the MCP scene and node commands.
- Godot registers only one script class per `.cs` file: the class with the file name. Put each `[GlobalClass]` class in its own file.
- `update_property` keeps string values as literal text. Do not put quotes around them.
- `delete_scene` does not delete a scene that is open. Close its tab first (`EditorInterface.close_scene()` in `execute_editor_script`).
- To install the server on a new machine, run `node build/setup.js install` in the server folder. `node_modules/` is not in git.
- Do not start a headless Godot **editor** (`--editor`, `--build-solutions`, `--import`) while the user's editor is open. The MCP plugin in the headless editor removes its autoloads from `project.godot` when it quits, and the game then starts without them. `scripts/build.sh` skips the Godot build in this case. A headless **game** run (`godot --headless --path project res://scenes/X.tscn --quit-after N`) is safe.
- `project set-setting` of the CLI reads a value that starts with `--` as its own option (it stored "true"). Set such values with `node scripts/godot-rpc.mjs set_project_setting '{"key":"...","value":"...","type":"string"}'`.
- If `project.godot` loses the `MCP*` autoloads, restore them with `project set-setting --setting autoload/<Name> --value "*res://addons/godot_mcp/<file>.gd" --type string`.
- `input key` sets only the logical `keycode`. The game reads physical keys, so it does not see these events. To simulate a key, use `runtime exec` with an `InputEventKey` that sets `physical_keycode`, then `Input.parse_input_event(e)`.
- C# properties keep their C# names in Godot. In GDScript and in MCP property commands, use `Size`, not `size` (for example `node.set("Size", Vector2i(10, 10))`). Built-in Godot properties keep their snake_case names (`position`).
- To test tool scripts without a scene change, build a node tree in memory in `editor exec` (add it under `EditorInterface.get_base_control()`), check it, and `queue_free()` it at the end.
- A script attached to a node that is already in the tree gets no enter-tree notification, so its `_Draw` may not run. Reload the saved scene (`EditorInterface.reload_scene_from_path(path)` in `editor exec`) to see the real editor view. The reload also uses the newest C# assembly.
- `EditorInterface.get_resource_filesystem().scan()` in `editor exec` finds new `.cs` files, but it logs harmless `progress_dialog.cpp` errors. Use it only when a new script class must be registered.
- In `editor exec` and `runtime exec`, return output with `_mcp_print(...)`. A plain `print()` is not returned.
- Do not use `await` in `runtime exec`. The command reports "Command crashed". Send each input event with a separate `runtime exec` call.
- To get a game screenshot, run `node scripts/godot-rpc.mjs get_game_screenshot '{}'`. The result is JSON with a PNG in `image_base64`. Decode it to a file in the scratchpad, then read the file.
- To test the app flow with a debug setting (for example `SessionType`), set the export on the running `Main` node with `runtime exec` before the match starts. Then the scene files do not change.
- In shell scripts, do not use the variable name `UID` (it is read-only in bash and holds the user id).
- UI built with the MCP: a `Control` child that must fill its parent needs `layout_mode = 1` and the `full_rect` anchor preset (`node scripts/godot-rpc.mjs set_anchor_preset '{"node_path":"X","preset":"full_rect"}'`).
- Each CLI call takes several seconds. Do not use CLI timing to check frame-exact behavior; use unit tests for that.
- `scripts/mcp-play-match.sh` (or `scene play` of a scene) makes that scene the current scene of the editor. Open the scene you want to change again, and check `scene_path`, before you change or save anything.
- To review an animation pose in the running game: pause the tree (`get_tree().paused = true`), set the fighter's `AnimationPlayer` to `PROCESS_MODE_ALWAYS`, `play(name)` and `seek(frame / 60.0, true)`, call `queue_redraw()` on the `StickFigure` (its `_Process` does not run while paused), then take the screenshot. Zoom with a temporary `Camera2D` added in `runtime exec`.
- To screenshot a short moment in the running game (for example an attack's active frames), slow the game with `Engine.time_scale = 0.02` in `runtime exec`. `MatchRunner` uses the frame delta, so the simulation slows down too.
- To start the game, use the editor (`scripts/mcp-play-match.sh` or `scene play`). Do not use `scripts/run-match.sh` or a shell `godot` command. The editor embeds the game in its Game tab, so the game does not open a window on top of the user's work.
- The user's desktop is COSMIC (Wayland). COSMIC opens new windows on the screen with the cursor, and ignores `--screen` and `--position`. Game embedding works only with the editor setting "Prefer Wayland" (`run/platforms/linuxbsd/prefer_wayland`). With X11, COSMIC does not let the editor embed the game window.
- The embedded game window has the size of the Game tab. The user set the tab to 1152x648. Screenshot sizes change if the editor layout changes.

Tool usage instructions from the MCP package:

@../mcp/godot-mcp-pro-v1.17.1/instructions/CLAUDE.md

## Build and test

Use the scripts in `scripts/` (run them from any folder):

- `scripts/build.sh` — Runs `dotnet build` on `FightingGame.sln`, then a Godot headless build (the same as the editor Build button). Use `--no-godot` to skip the Godot build. It skips the Godot build when the editor is open with this project.
- `scripts/test.sh` — Runs all unit tests. Extra arguments go to `dotnet test` (for example `--filter FixedTests`).
- `scripts/godot-rpc.mjs` — Sends one command to the Godot MCP editor plugin: `node scripts/godot-rpc.mjs <method> '<params JSON>'`. Use it for plugin commands that the CLI does not offer, for example `set_input_action` (input actions) and `get_input_actions`. The plugin commands are in `project/addons/godot_mcp/commands/`.
- `scripts/run-match.sh` — Runs the match scene directly (no main menu) in a game window, for the user. Match arguments: `--players=N` (1 to 4; slots without a real device get an idle device), `--seed=N`, `--session=local|synctest|loopback`, `--latency=MS`, `--jitter=MS`, `--loss=PERCENT`. A game started from the shell does not accept MCP runtime commands.
- `scripts/mcp-play-match.sh` — The open editor runs the match scene directly, with the same match arguments. Use it for tests with MCP runtime commands (screenshots, input). It sets `editor/run/main_run_args`, starts the game, and sets the setting back to empty.
- `scripts/godot-path.sh` — Prints the Godot executable path. It reads `$GODOT_BIN`, then `godotTools.editorPath.godot4` in `fighting-game.code-workspace`.

Run `scripts/build.sh` and `scripts/test.sh` after each code change.

Only the .NET 10 runtime is installed. The test project targets `net8.0` and uses `RollForward=Major` to run on .NET 10.

## Design document

- Follow the design in `project.md` (repository root). It is the source of truth for the architecture and the milestones.
- When a design decision changes or is added, update `project.md` (and its Decision Log) in the same change.
- At the start of a session, read section 0 ("Current Status and Next Steps") of `project.md`. Confirm the next step with the user before you write code.
- At the end of a session, update section 0.

## Determinism rules

- The simulation code is in the class library `project/simulation/FightingGame.Simulation.csproj`. It must not reference Godot. Do not add Godot packages to it.
- Put Godot code (views, UI, authoring nodes, Godot transports) in `project/src/`.
- Put tests in `tests/` and tools in `tools/` (outside `project/`).
- Do not use `float`, `double`, `System.Random`, `DateTime`, or static mutable state in the simulation.
- Do not iterate `Dictionary` or `HashSet` in the simulation.
- Use the fixed-point types (`Fixed`, `FixedVector2`, `FixedAABB`) for all simulation values.
- Keep the world state as plain value types, so it can be saved, restored, and hashed.
