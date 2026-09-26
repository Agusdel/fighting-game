# Tools Reference

All 187 tools, generated from the server's tool definitions (`scripts/gen-tools-reference.mjs`). Tools marked Godot 4.6+ or 4.7+ answer with an error naming the required version on older Godot.

## Contents

- [Project (10)](#project-10)
- [Scene (10)](#scene-10)
- [Node (17)](#node-17)
- [Script (9)](#script-9)
- [Editor (15)](#editor-15)
- [Input (5)](#input-5)
- [Runtime (20)](#runtime-20)
- [Input Map (2)](#input-map-2)
- [Animation (6)](#animation-6)
- [AnimationTree (9)](#animationtree-9)
- [Audio (6)](#audio-6)
- [Batch & Refactoring (7)](#batch--refactoring-7)
- [Export (4)](#export-4)
- [Navigation (5)](#navigation-5)
- [Particles (5)](#particles-5)
- [Physics (6)](#physics-6)
- [Profiling (2)](#profiling-2)
- [Resource (4)](#resource-4)
- [3D Scene (7)](#3d-scene-7)
- [Shader (6)](#shader-6)
- [Testing & QA (6)](#testing--qa-6)
- [Theme & UI (8)](#theme--ui-8)
- [TileMap (6)](#tilemap-6)
- [Analysis (6)](#analysis-6)
- [Android (3)](#android-3)
- [Headless (3)](#headless-3)

## Project (10)

### get_project_info

Get Godot project metadata including name, version, viewport settings, renderer, and autoloads

No parameters.

### get_filesystem_tree

Get the project's file/directory tree with optional filtering by extension (e.g. *.gd, *.tscn)

| Parameter | Type | Required | Description |
|---|---|---|---|
| `path` | string | no | Root path to scan (default: res://) |
| `filter` | string | no | Glob filter pattern (e.g. '*.gd', '*.tscn') |
| `max_depth` | number | no | Maximum directory depth to scan (default: 10) |

### search_files

Search for files by name using fuzzy matching or glob patterns

| Parameter | Type | Required | Description |
|---|---|---|---|
| `query` | string | yes | Search query (fuzzy match or glob pattern) |
| `path` | string | no | Root path to search (default: res://) |
| `file_type` | string | no | Filter by file extension (e.g. 'gd', 'tscn') |
| `max_results` | number | no | Maximum results to return (default: 50) |

### search_in_files

Search for text content inside project files (grep-like). Skips every dot-prefixed file and directory, and skips addons/ unless include_addons is true — though an explicit path inside addons/ is searched regardless.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `query` | string | yes | Text to search for (plain text or regex pattern) |
| `path` | string | no | Root path to search (default: res://) |
| `regex` | boolean | no | Use regex matching (default: false) |
| `file_type` | string | no | Filter by file extension (e.g. 'gd', 'tscn'). Without it, only these extensions are searched: gd, tscn, tres, cfg, godot, gdshader, md, txt, json — pass file_type explicitly for anything else. |
| `max_results` | number | no | Maximum results to return (default: 50) |
| `include_addons` | boolean | no | Also search addons/ (default: false). Needed when debugging vendored third-party addon code. |

### get_project_settings

Read project.godot settings by section or specific key. Set non_default_only=true to list only settings that differ from the engine default — a compact view of what a project actually configures.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `section` | string | no | Settings section prefix (e.g. 'display/window') |
| `key` | string | no | Specific setting key (e.g. 'display/window/size/viewport_width') |
| `non_default_only` | boolean | no | Only return settings whose value differs from the engine default (default: false). Custom keys always count as non-default. The response adds a 'defaults' map with each returned setting's default value. Combine with 'section' to narrow further. Ignored when 'key' is given. |

### set_project_setting

Set a project setting value (e.g. viewport size, main scene). Saves to project.godot via the editor API. For an existing key the declared type is preserved, so a string setting stays a String even when its value looks numeric. Container types are set by passing an array. Refuses to write 'editor_plugins/enabled' — use EditorInterface.set_plugin_enabled() instead.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `key` | string | yes | Setting key (e.g. 'display/window/size/viewport_width', 'application/run/main_scene') |
| `value` | string \| number \| boolean \| array \| object | yes | Value to set. Arrays build container types: [x, y] for vector2/vector2i, [r, g, b, a] for color, ['a', 'b'] for packed_string_array, and nested arrays for packed_vector2_array ([[0, 0], [1, 2]]) and packed_color_array ([[1, 0, 0, 1]]). Values that cannot be represented in the target type are rejected rather than coerced. |
| `type` | "string" \| "string_name" \| "int" \| "float" \| "bool" \| "vector2" \| "vector2i" \| "vector3" \| "vector3i" \| "rect2" \| "rect2i" \| "color" \| "array" \| "dictionary" \| "packed_string_array" \| "packed_int32_array" \| "packed_int64_array" \| "packed_float32_array" \| "packed_float64_array" \| "packed_vector2_array" \| "packed_color_array" | no | Force the stored type. Required to create a NEW key with a non-default type, and the only way to force a String for a numeric-looking value. Existing keys keep their declared type when omitted. |

### uid_to_project_path

Convert a Godot UID (uid://...) to a project resource path (res://...)

| Parameter | Type | Required | Description |
|---|---|---|---|
| `uid` | string | yes | The UID string (e.g. 'uid://abc123') |

### project_path_to_uid

Convert a project resource path (res://...) to its UID (uid://...)

| Parameter | Type | Required | Description |
|---|---|---|---|
| `path` | string | yes | The resource path (e.g. 'res://scenes/player.tscn') |

### add_autoload

Add an autoload (singleton) to the project. The script/scene will be auto-loaded when the project starts.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `name` | string | yes | Autoload name (e.g. 'GameManager', 'AudioManager') |
| `path` | string | yes | Path to the script or scene file (e.g. 'res://scripts/autoload/game_manager.gd') |

### remove_autoload

Remove an autoload (singleton) from the project settings

| Parameter | Type | Required | Description |
|---|---|---|---|
| `name` | string | yes | Autoload name to remove (e.g. 'GameManager') |

## Scene (10)

### get_scene_tree

Get the live scene tree of the currently edited scene, showing all nodes, types, and hierarchy

| Parameter | Type | Required | Description |
|---|---|---|---|
| `max_depth` | number | no | Max tree depth to return (-1 for unlimited) |

### get_scene_file_content

Read the raw .tscn file content of a scene

| Parameter | Type | Required | Description |
|---|---|---|---|
| `path` | string | yes | Path to the scene file (e.g. 'res://scenes/main.tscn') |

### create_scene

Create a new scene file with a specified root node type. Refuses to overwrite an existing scene unless force is set.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `path` | string | yes | Path for the new scene (e.g. 'res://scenes/enemy.tscn') |
| `root_type` | string | no | Root node type (default: Node2D). Must be a concrete Node subclass. Examples: Node2D, Node3D, Control, CharacterBody2D |
| `root_name` | string | no | Root node name (defaults to filename) |
| `force` | boolean | no | Overwrite the scene if it already exists (default: false) |

### open_scene

Open a scene file in the Godot editor

| Parameter | Type | Required | Description |
|---|---|---|---|
| `path` | string | yes | Path to the scene file (e.g. 'res://scenes/main.tscn') |

### delete_scene

Delete a scene file from the project. Only .tscn/.scn files are accepted, and a scene open in the editor is refused.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `path` | string | yes | Path to the scene file to delete (.tscn or .scn) |

### add_scene_instance

Add an existing scene as a child node (instancing) in the current scene

| Parameter | Type | Required | Description |
|---|---|---|---|
| `scene_path` | string | yes | Path to the scene to instance (e.g. 'res://scenes/enemy.tscn') |
| `parent_path` | string | no | Parent node path (default: root '.') |
| `name` | string | no | Custom name for the instance |

### play_scene

Run a scene in the Godot editor (main scene, current scene, or specific path)

| Parameter | Type | Required | Description |
|---|---|---|---|
| `mode` | string | no | 'main' (default), 'current', or a scene file path |

### stop_scene

Stop the currently playing scene

No parameters.

### save_scene

Save the currently edited scene to disk

| Parameter | Type | Required | Description |
|---|---|---|---|
| `path` | string | no | Optional path to save to (defaults to current scene path) |

### get_scene_exports

Get all @export variables from all scripted nodes in a scene file. Useful for inspecting configurable parameters without opening the scene.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `path` | string | yes | Path to the scene file (e.g. 'res://scenes/enemy.tscn') |

## Node (17)

### add_node

Add a new node to the current scene. Supports built-in Godot types and script-defined classes (class_name).

| Parameter | Type | Required | Description |
|---|---|---|---|
| `type` | string | yes | Node type — built-in (e.g. 'Sprite2D', 'Camera2D') or script class_name (e.g. 'HoverDetector', 'StationBuilder') |
| `parent_path` | string | no | Parent node path (default: root '.') |
| `name` | string | no | Node name |
| `properties` | object | no | Properties to set (e.g. {"position": "Vector2(100, 200)"}) |

### delete_node

Delete a node from the current scene (supports undo)

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the node to delete |

### duplicate_node

Duplicate a node and all its children in the current scene

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the node to duplicate |
| `name` | string | no | Name for the duplicate (default: original_copy) |

### move_node

Move/reparent a node to a new parent in the scene tree

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the node to move |
| `new_parent_path` | string | yes | Path to the new parent node |

### update_property

Change a property on any node. Supports Vector2, Color, and other Godot types via string parsing.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the target node |
| `property` | string | yes | Property name (e.g. 'position', 'modulate', 'visible') |
| `value` | any | no | New value. Strings are auto-parsed: 'Vector2(10,20)', 'Color(1,0,0)', '#ff0000', etc. |

### get_node_properties

Get all editor-visible properties of a node with their current values

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the node |
| `category` | string | no | Filter by property category prefix (e.g. 'transform', 'texture') |

### add_resource

Add a resource (Shape2D, Material, Texture, etc.) to a node's property

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the target node |
| `property` | string | yes | Property to set the resource on (e.g. 'shape', 'material', 'texture') |
| `resource_type` | string | yes | Resource class name (e.g. 'RectangleShape2D', 'CircleShape2D', 'StandardMaterial3D') |
| `resource_properties` | object | no | Properties to set on the created resource |

### set_anchor_preset

Set a Control node's anchor preset (e.g. full_rect, center, top_left)

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the Control node |
| `preset` | string | yes | Anchor preset name: top_left, top_right, bottom_left, bottom_right, center_left, center_top, center_right, center_bottom, center, left_wide, top_wide, right_wide, bottom_wide, vcenter_wide, hcenter_wide, full_rect |
| `keep_offsets` | boolean | no | Keep current offsets (default: false) |

### rename_node

Rename a node in the current scene

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the node to rename |
| `new_name` | string | yes | New name for the node |

### connect_signal

Connect a signal from one node to a method on another node. The connection is persistent (saved into the .tscn on save_scene).

| Parameter | Type | Required | Description |
|---|---|---|---|
| `source_path` | string | yes | Path to the source node (emitter) |
| `signal_name` | string | yes | Signal name to connect |
| `target_path` | string | yes | Path to the target node (receiver) |
| `method_name` | string | yes | Method name on target to call |
| `deferred` | boolean | no | Use a deferred connection (CONNECT_DEFERRED) |
| `one_shot` | boolean | no | Disconnect automatically after the first emission (CONNECT_ONE_SHOT) |

### disconnect_signal

Disconnect a signal connection between two nodes

| Parameter | Type | Required | Description |
|---|---|---|---|
| `source_path` | string | yes | Path to the source node (emitter) |
| `signal_name` | string | yes | Signal name to disconnect |
| `target_path` | string | yes | Path to the target node (receiver) |
| `method_name` | string | yes | Method name on target |

### get_node_groups

Get all groups a node belongs to (excludes internal groups starting with '_')

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the node |

### set_node_groups

Set the groups a node belongs to. Computes diff with current groups and adds/removes as needed.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the node |
| `groups` | array | yes | Desired list of group names (replaces current groups) |

### find_nodes_in_group

Find all nodes in the current scene that belong to a specific group

| Parameter | Type | Required | Description |
|---|---|---|---|
| `group` | string | yes | Group name to search for |

### get_editor_selection

Get the nodes currently selected in the editor Scene dock

| Parameter | Type | Required | Description |
|---|---|---|---|
| `top_only` | boolean | no | Return only the topmost selected nodes, excluding a node whose parent is already selected (default: false) |

### select_nodes

Select one or more nodes in the editor Scene dock, optionally focusing and inspecting them

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | no | Path to a single node to select |
| `node_paths` | array | no | Paths to multiple nodes to select (use instead of node_path) |
| `mode` | "replace" \| "add" \| "remove" | no | How to apply the selection (default: replace) |
| `inspect` | boolean | no | Show the node in the Inspector (only applied when a single node is selected; default: true) |
| `focus` | boolean | no | Focus the node in the Scene dock (only applied when a single node is selected; default: follows inspect) |
| `for_property` | string | no | Inspector property to focus on |
| `inspector_only` | boolean | no | Show in Inspector without changing the edited node (default: false) |

### clear_editor_selection

Clear the current editor Scene-dock selection

No parameters.

## Script (9)

### list_scripts

List all GDScript/C#/shader files in the project with class info

| Parameter | Type | Required | Description |
|---|---|---|---|
| `path` | string | no | Root path to search (default: res://) |
| `recursive` | boolean | no | Search recursively (default: true) |

### read_script

Read the full content of a GDScript file

| Parameter | Type | Required | Description |
|---|---|---|---|
| `path` | string | yes | Path to the script (e.g. 'res://scripts/player.gd') |

### create_script

Create a new GDScript file with optional content or auto-generated template. Restricted to .gd/.cs paths. Refuses to overwrite a script that is currently open in Godot's script editor unless force=true is set. On Godot 4.7+, when a forced write hits an open script, the editor buffer is reloaded from disk and the response reports editor_buffer_reloaded; if the buffer had unsaved edits Godot keeps them and the response says so (editor_buffer_reloaded: false + editor_buffer_warning).

| Parameter | Type | Required | Description |
|---|---|---|---|
| `path` | string | yes | Path for the new script (e.g. 'res://scripts/enemy_ai.gd'). Must be a .gd or .cs file. |
| `content` | string | no | Full script content. If empty, generates a template. |
| `extends` | string | no | Base class (default: 'Node'). Only used for template generation. |
| `class_name` | string | no | Class name to add. Only used for template generation. |
| `force` | boolean | no | Override the open-script-editor guard and write anyway. Use only when no editor buffer holds unsaved changes for the target path. |

### edit_script

Edit a script using search-and-replace, full content replacement, line insertion, or 1-based inclusive line-range replacement. Restricted to .gd/.cs paths. Refuses to write a script that is currently open in Godot's script editor unless force=true is set. On Godot 4.7+, a forced edit of an open script reloads the editor buffer from disk and reports editor_buffer_reloaded (false + editor_buffer_warning when the buffer had unsaved edits Godot kept). Content is written VERBATIM — indentation is never adjusted, so supply it matching the file. The response reports valid_after_edit, parse_errors if the edit broke the file, and indentation_warning if the new content's indent style differs from the file's.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `path` | string | yes | Path to the script to edit. Must be a .gd or .cs file. |
| `replacements` | array | no | Array of search-and-replace operations |
| `content` | string | no | Full replacement content (replaces entire file), or replacement lines when combined with start_line/end_line |
| `insert_at_line` | number | no | Line number to insert text at (0-indexed) |
| `text` | string | no | Text to insert (used with insert_at_line) |
| `start_line` | number | no | 1-based inclusive starting line for range replacement (used with content) |
| `end_line` | number | no | 1-based inclusive ending line for range replacement (defaults to start_line) |
| `force` | boolean | no | Override the open-script-editor guard and write anyway. |

### attach_script

Attach a GDScript to a node in the current scene

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the target node |
| `script_path` | string | yes | Path to the script file (e.g. 'res://scripts/player.gd') |

### validate_script

Validate a GDScript file by attempting to compile it. Returns valid: true/false, plus parse_errors with the real parser message on failure. Rarely returns valid: null with indeterminate: true, when Godot skipped the compile because live instances hold the script — that means undetermined, NOT broken.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `path` | string | yes | Path to the script to validate (e.g. 'res://scripts/player.gd') |

### close_script

Close a script's tab in Godot's script editor. Requires Godot 4.7+ (returns an error on older versions). Refuses when the script's editor buffer has unsaved changes unless discard_unsaved=true, because closing throws those changes away without asking. Use save_all first to keep them.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `path` | string | yes | Path of the open script to close (e.g. 'res://scripts/player.gd'). Must be a .gd or .cs file. |
| `discard_unsaved` | boolean | no | Close even if the editor buffer has unsaved changes, discarding them (default: false) |

### reload_open_scripts

Reload all scripts open in Godot's script editor from disk, e.g. after files were changed outside the editor. Requires Godot 4.7+ (returns an error on older versions). Buffers with unsaved edits are not reloaded; they are listed in kept_unsaved. Refuses when a buffer still holds edits from a failed save (Godot reports those as clean and would reload over them) unless discard_unsaved=true.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `discard_unsaved` | boolean | no | Reload even over buffers whose edits could not be saved earlier, losing those edits (default: false) |

### get_open_scripts

Get a list of scripts currently open in the Godot script editor

No parameters.

## Editor (15)

### get_editor_errors

Get recent errors and stack traces from the Godot editor log

| Parameter | Type | Required | Description |
|---|---|---|---|
| `max_lines` | number | no | Maximum log lines to scan for errors (default: 50) |

### get_output_log

Read the full Godot editor Output panel content. Unlike get_editor_errors which filters for errors only, this returns all output including print() statements and warnings.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `max_lines` | number | no | Maximum number of lines to return from the end (default: 100) |
| `filter` | string | no | Filter lines containing this substring (case-sensitive) |

### get_editor_screenshot

Capture a screenshot of the Godot editor's 2D/3D viewport

| Parameter | Type | Required | Description |
|---|---|---|---|
| `save_path` | string | no | Optional res:// or user:// path to save the screenshot as PNG file (e.g. 'res://screenshot.png'). When provided, the image is saved to disk and the file path is returned instead of base64 data. |

### get_game_screenshot

Capture a single screenshot of the running game (requires a scene to be playing). Good for checking static visual state (UI layout, scene composition, colors). For verifying animations or movement, use capture_frames instead — a single screenshot cannot confirm whether an animation is playing.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `save_path` | string | no | Optional res:// or user:// path to save the screenshot as PNG file (e.g. 'res://screenshot.png'). When provided, the image is saved to disk and the file path is returned instead of base64 data. |

### execute_editor_script

Execute arbitrary GDScript code inside the Godot editor process. Use _mcp_print() to output values. As an accident guard — NOT a security boundary — the submitted source is string-matched for direct file/resource write APIs (ResourceSaver.save, FileAccess WRITE, ProjectSettings.save, ConfigFile.save, DirAccess mutations) and refused, because those bypass the per-command open-resource guards. The check inspects text only: code that builds such a call dynamically, or uses a destructive API not on the list, runs regardless. Prefer the dedicated tools (save_scene, create_script, etc.) for writes, or pass allow_unsafe_editor_io=true once you have verified no open editor resource will be overwritten.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `code` | string | yes | GDScript code to execute. Use _mcp_print(value) to capture output. The code runs inside a run() function with access to the full editor API. |
| `allow_unsafe_editor_io` | boolean | no | Override the file-write safety guard. Only set this when you are certain no open scene/script/shader will be overwritten by the script. Prefer the dedicated MCP tools for ordinary save flows. |

### clear_output

Clear the Godot editor output panel

No parameters.

### get_signals

Get all signals of a node, including current connections

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the node to inspect |

### reload_plugin

Reload the Godot MCP Pro plugin (disable/re-enable). Connection will briefly drop and auto-reconnect. NOTE: This does NOT reload GDScript preload() caches. If you changed GDScript command files, use execute_editor_script with 'EditorInterface.restart_editor(true)' instead for a full editor restart.

No parameters.

### reload_project

Rescan the Godot project filesystem and reload changed scripts (no reconnection needed)

No parameters.

### compare_screenshots

Compare two screenshots pixel-by-pixel and return a diff analysis. Returns changed pixel count, diff percentage, and a highlighted diff image. Useful for visual regression testing. Accepts file paths (res://, user://) or base64 PNG strings.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `image_a` | string | yes | First image: file path (e.g. 'user://screenshot_a.png') or base64 PNG string |
| `image_b` | string | yes | Second image: file path (e.g. 'user://screenshot_b.png') or base64 PNG string |
| `threshold` | number | no | Color difference threshold (0-255, default: 10). Pixels with max channel difference below this are considered identical. |

### set_auto_dismiss

Enable or disable automatic dismissal of blocking editor dialogs (e.g. 'Reload from disk?', 'Save changes?'). Enable this before operations that modify files externally, and disable when done. Disabled by default.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `enabled` | boolean | yes | true to enable auto-dismiss, false to disable |

### get_editor_camera

Get the current 3D editor viewport camera position, rotation, and FOV. Use this to understand the current view before taking editor screenshots. On Godot 4.6+ the response also includes snap_3d {enabled, translate, rotate_degrees, scale_percent} from the 3D viewport's snap settings; the key is omitted on older versions.

No parameters.

### get_unsaved_state

Report which open scenes and scripts have unsaved changes in the Godot editor. Returns unsaved_scenes, unsaved_scripts, open_scenes, open_scripts and godot_version. unsaved_scenes and unsaved_scripts require Godot 4.7+; on older versions they are null (unknown — NOT 'everything saved') with a note. Check this before writing files that are open in the editor, or before save_all.

No parameters.

### save_all

Save all open scenes and all modified script-editor buffers, then report what was unsaved before and what is still unsaved afterwards. Scene saving works on every supported Godot version; script saving requires Godot 4.7+ (on older versions scripts_saved is null with a note, and scripts=true alone returns an error). Script buffers are written as they are: if a script file changed on disk after its buffer was edited, the buffer overwrites it. Scenes that were never saved (no path) are skipped — use save_scene with a path for those.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `scenes` | boolean | no | Save all open scenes (default: true) |
| `scripts` | boolean | no | Save all modified script-editor buffers (default: true). Requires Godot 4.7+. |

### set_editor_camera

Move the 3D editor viewport camera to a specific position and orientation. Use this to frame a view before taking editor screenshots to validate changes visually.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `position` | object | no | Camera world position |
| `rotation_degrees` | object | no | Camera rotation in degrees |
| `look_at` | object | no | Point to look at (overrides rotation_degrees if both set) |
| `fov` | number | no | Field of view in degrees (default: 75) |

## Input (5)

### simulate_key

Simulate a keyboard key press or release in the running game. Use `duration` to hold a key for a set time (auto-releases after). Without duration: keys are NOT auto-released — you must explicitly call with pressed=false to release them. Events carry the device id of real keyboard input (InputEvent.DEVICE_ID_KEYBOARD = 16 on Godot 4.7+, 0 before), so game code that checks event.device treats them as keyboard.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `keycode` | string | yes | Key constant (e.g. 'KEY_SPACE', 'KEY_W', 'KEY_ESCAPE') |
| `pressed` | boolean | no | true for press, false for release (default: true) |
| `duration` | number | no | Hold duration in seconds (e.g. 1.5). Key is pressed, held for this duration, then auto-released. Cannot be used with pressed=false. |
| `shift` | boolean | no | Shift modifier (default: false) |
| `ctrl` | boolean | no | Ctrl modifier (default: false) |
| `alt` | boolean | no | Alt modifier (default: false) |

### simulate_mouse_click

Simulate a mouse button click at a position in the running game. By default sends both press and release (auto_release) so UI buttons work correctly. Like real mouse input, events use device id InputEvent.DEVICE_ID_MOUSE (32) on Godot 4.7+ and 0 before.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `x` | number | no | X position in viewport (default: 0) |
| `y` | number | no | Y position in viewport (default: 0) |
| `button` | number | no | Mouse button index: 1=left, 2=right, 3=middle (default: 1) |
| `pressed` | boolean | no | true for press, false for release (default: true) |
| `double_click` | boolean | no | Double click (default: false) |
| `auto_release` | boolean | no | Auto-send release after press so buttons fire (default: true). Set false for drag operations. |

### simulate_mouse_move

Simulate mouse movement in the running game. Use x/y for absolute viewport positioning (UI interaction), or relative_x/relative_y for relative motion (camera rotation in 3D games, FPS-style look). For 3D camera rotation: relative_x rotates yaw (negative = look left, positive = look right), relative_y rotates pitch (negative = look up, positive = look down). Typical values: 200-400px for a ~90° turn. Use navigate_to tool to calculate exact relative_x needed to face a target.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `x` | number | no | Absolute X position in viewport (for UI interaction) |
| `y` | number | no | Absolute Y position in viewport (for UI interaction) |
| `relative_x` | number | no | Relative X movement in pixels. For 3D camera: negative = look left, positive = look right. ~400px ≈ 180° turn |
| `relative_y` | number | no | Relative Y movement in pixels. For 3D camera: negative = look up, positive = look down |
| `button_mask` | number | no | Mouse button mask to simulate drag. 1=left button held, 2=right button held, 4=middle button held. Required for drag operations like camera pan. (default: 0) |
| `unhandled` | boolean | no | Force event to bypass GUI layer and go directly to _unhandled_input(). Auto-enabled when button_mask > 0. Use for camera pan/drag when UI overlays consume mouse events. (default: false) |

### simulate_action

Simulate a Godot Input Action (e.g. 'jump', 'move_left') in the running game

| Parameter | Type | Required | Description |
|---|---|---|---|
| `action` | string | yes | Action name as defined in Input Map (e.g. 'jump', 'move_left') |
| `pressed` | boolean | no | true for press, false for release (default: true) |
| `strength` | number | no | Action strength 0.0-1.0 (default: 1.0) |

### simulate_sequence

Simulate a sequence of input events with optional frame delays between them. Useful for complex input patterns like press W → wait 30 frames → press Space → wait → release all. After the sequence, use capture_frames to verify the visual result.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `events` | array | yes | Array of input events to send |
| `frame_delay` | number | no | Frames to wait between events (default: 1, 0 = all in one frame) |

## Runtime (20)

### get_game_scene_tree

Get the scene tree of the currently running game (requires a scene to be playing). Supports filtering by script path, node type, or name.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `max_depth` | number | no | Maximum tree depth (-1 for unlimited, default: -1) |
| `script_filter` | string | no | Only include nodes whose script path contains this string (e.g. 'enemy' matches 'enemy.gd', 'enemy_drone.gd') |
| `type_filter` | string | no | Only include nodes of this Godot class (e.g. 'CharacterBody2D', 'Area2D') |
| `named_only` | boolean | no | If true, exclude nodes with auto-generated names (names starting with '@'). Default: false |

### get_game_node_properties

Get properties of a node in the running game (requires a scene to be playing)

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Absolute node path in the running game (e.g. '/root/Main/Player') |
| `properties` | array | no | Specific property names to read (default: all editor-visible properties) |

### set_game_node_property

Set a property on a node in the running game (requires a scene to be playing). Useful for live-tweaking values like position, speed, health, etc.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Absolute node path in the running game (e.g. '/root/Main/Player') |
| `property` | string | yes | Property name to set (e.g. 'position', 'speed', 'health') |
| `value` | string \| number \| boolean \| object | yes | Value to set. Accepts: strings with auto-parsing ('Vector2(100,200)', '#ff0000'), numbers, booleans, or JSON objects for vectors/colors ({"x":5,"y":3,"z":10} for Vector3, {"x":100,"y":200} for Vector2, {"r":1,"g":0,"b":0,"a":1} for Color) |

### execute_game_script

Execute arbitrary GDScript code inside the running game process. Use _mcp_print() to output values. Has access to the live scene tree and all game nodes.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `code` | string | yes | GDScript code to execute in the running game. Use _mcp_print(value) to capture output. Code runs inside a run() function with access to the live game scene tree. |

### capture_frames

Capture multiple screenshots at regular frame intervals from the running game. Returns base64 PNG images. Use this to verify animations are playing correctly — if character poses differ across frames, the animation is working; if all frames show the same pose (e.g. T-pose), animation loading failed. Also useful for verifying movement, physics, and any time-based behavior. Prefer this over get_game_screenshot when you need to confirm something is changing over time.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `count` | number | no | Number of frames to capture (1-30, default: 5) |
| `frame_interval` | number | no | Frames to wait between captures (default: 10, i.e. ~6 captures/sec at 60fps) |
| `half_resolution` | boolean | no | Halve resolution to reduce data size (default: true) |
| `node_data` | object | no | Optional: capture node property data alongside each frame for debugging (position, velocity, etc.) |

### record_frames

Record many screenshots to files on disk for long-running debug observation. Unlike capture_frames (which returns base64 images directly), this saves PNG files to user://mcp_recorded_frames/ and returns file paths. Use this when you need more than 30 frames or want to observe behavior over a longer period without flooding the context with image data.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `count` | number | no | Number of frames to capture (1-600, default: 30) |
| `frame_interval` | number | no | Frames to wait between captures (default: 10, i.e. ~6 captures/sec at 60fps) |
| `half_resolution` | boolean | no | Halve resolution to reduce file size (default: true) |
| `node_data` | object | no | Optional: capture node property data alongside each frame for debugging |

### monitor_properties

Record property values over multiple frames from the running game. Returns a timeline of samples. Great for verifying movement (position changing), animation state (current_animation property), physics behavior (velocity), and debugging time-dependent issues.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Absolute node path in the running game (e.g. '/root/Main/Player') |
| `properties` | array | yes | Property names to monitor (e.g. ['position', 'velocity']) |
| `frame_count` | number | no | Number of samples to collect (1-600, default: 60) |
| `frame_interval` | number | no | Frames to wait between samples (default: 1, every frame) |

### watch_signals

Monitor signal emissions on specified nodes in the running game for a duration. Returns a timestamped log of every signal fired — great for debugging event flow, verifying signal connections, and understanding runtime behavior.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_paths` | array | yes | Absolute node paths to watch (e.g. ['/root/Main/Player', '/root/Main/Enemy']) |
| `signal_filter` | array | no | Only watch signals containing these substrings (e.g. ['health', 'died']). Omit to watch all signals. |
| `duration_ms` | number | no | How long to watch in milliseconds (500-30000, default: 5000) |

### start_recording

Start recording all input events (keyboard, mouse, actions) in the running game. Use stop_recording to get the recorded events.

No parameters.

### stop_recording

Stop recording input events and return the recorded event timeline. Events include timestamps for replay.

No parameters.

### replay_recording

Replay a previously recorded input event sequence in the running game. Useful for regression testing — record a test once, replay it after code changes.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `events` | array | yes | Array of recorded event objects (from stop_recording output) |
| `speed` | number | no | Playback speed multiplier (default: 1.0, 2.0 = double speed) |

### find_nodes_by_script

Find all nodes in the running game whose script path contains a given string. Returns matching nodes with their properties.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `script` | string | yes | Script path substring to search for (e.g. 'enemy', 'player.gd') |
| `properties` | array | no | Specific property names to include for each match (default: all editor-visible properties) |

### get_autoload

Get properties of an autoload/singleton node in the running game. Quick access to global game state like GameManager, EventBus, etc.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `name` | string | yes | Autoload name (e.g. 'GameManager', 'EventBus', 'SaveManager') |
| `properties` | array | no | Specific property names to read (default: all editor-visible properties) |

### find_ui_elements

Find all visible UI elements (Button, Label, LineEdit, CheckBox, Slider, etc.) in the running game. Returns each element's text, type, position, and center point for clicking.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `type_filter` | string | no | Only return elements of this type (e.g. 'Button', 'Label', 'CheckBox'). Default: all types |

### click_button_by_text

Click a button in the running game by its text label. Finds the button, calculates its center, and simulates a full click (press + release). Much easier than manual coordinate-based clicking.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `text` | string | yes | Button text to search for (e.g. 'New Game', 'Start', 'OK') |
| `partial` | boolean | no | Allow partial text matching (default: true). If false, requires exact match. |

### wait_for_node

Wait until a node exists at the given path in the running game scene tree. Useful for waiting after scene transitions, node spawning, or UI state changes.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Absolute node path to wait for (e.g. '/root/Main/Player', '/root/Dungeon') |
| `timeout` | number | no | Maximum seconds to wait (default: 5.0) |
| `poll_frames` | number | no | Frames between each check (default: 5, i.e. ~12 checks/sec at 60fps) |

### find_nearby_nodes

Find all nodes within a radius of a position in the running game, sorted by distance. Useful for finding what's near the player (collectibles, enemies, interactables) without manually querying each node's position.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `position` | string \| object | yes | Origin position: either a node_path string (e.g. '/root/Main/Player') to use that node's global_position, or an {x, y, z} coordinate object |
| `radius` | number | no | Search radius in world units (default: 20.0) |
| `type_filter` | string | no | Only include nodes of this Godot class (e.g. 'Area3D', 'CharacterBody3D') |
| `group_filter` | string | no | Only include nodes in this group (e.g. 'enemies', 'collectibles') |
| `max_results` | number | no | Maximum number of results to return (default: 10) |

### navigate_to

Calculate navigation info from the player to a target in the running 3D game. Returns the world direction, camera-relative suggested WASD keys to press, camera yaw rotation needed (as mouse relative_x pixels for simulate_mouse_move), and estimated walk duration. Use this to plan movement instead of manually calculating directions.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `target` | string \| object | yes | Target: either a node_path string (e.g. '/root/Main/Crystal') or an {x, y, z} coordinate object |
| `player_path` | string | no | Player node path (default: '/root/Main/Player') |
| `camera_path` | string | no | Camera node path (default: auto-detect active Camera3D) |
| `move_speed` | number | no | Player movement speed in units/sec for duration estimation (default: 5.0) |

### move_to

Autopilot the player character to walk to a target position in the running 3D game. Handles camera rotation and forward movement internally at 60fps — completes in a single call with no manual simulate_key/simulate_mouse_move loops needed. The player's camera pivot is directly rotated toward the target, and W key is injected to walk. Much more reliable and efficient than navigate_to + manual input simulation.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `target` | string \| object | yes | Target: either a node_path string (e.g. '/root/Main/Crystal') or an {x, y, z} coordinate object |
| `player_path` | string | no | Player node path (default: '/root/Main/Player') |
| `camera_path` | string | no | Camera pivot node path (default: auto-detect SpringArm3D child of player, or active Camera3D parent) |
| `arrival_radius` | number | no | Stop when this close to target in world units (default: 1.5) |
| `timeout` | number | no | Maximum seconds before giving up (default: 15.0) |
| `run` | boolean | no | Hold Shift for running speed (default: false) |
| `look_at_target` | boolean | no | Rotate camera toward target while moving (default: true). Set false to walk forward without turning. |

### batch_get_properties

Get properties of multiple nodes at once in the running game. More efficient than calling get_game_node_properties multiple times.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `nodes` | array | yes | Array of nodes to query |

## Input Map (2)

### get_input_actions

Get all input actions defined in the project's Input Map with their key/button bindings

| Parameter | Type | Required | Description |
|---|---|---|---|
| `filter` | string | no | Filter action names containing this substring |
| `include_builtin` | boolean | no | Include built-in ui_* actions (default: false) |

### set_input_action

Create or update an input action with key/mouse/joypad bindings. Saves to project.godot and updates the runtime InputMap.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `action` | string | yes | Action name (e.g. 'move_left', 'jump', 'attack') |
| `events` | array | yes | Array of input event bindings |
| `deadzone` | number | no | Deadzone for analog inputs (default: 0.5) |

## Animation (6)

### list_animations

List all animations in an AnimationPlayer node

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the AnimationPlayer node |

### create_animation

Create a new animation in an AnimationPlayer

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the AnimationPlayer node |
| `name` | string | yes | Name for the new animation |
| `length` | number | no | Animation length in seconds (default: 1.0) |
| `loop_mode` | number | no | Loop mode: 0=none, 1=linear, 2=pingpong (default: 0) |

### add_animation_track

Add a track to an animation (value, position, rotation, scale, method, bezier)

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the AnimationPlayer node |
| `animation` | string | yes | Animation name |
| `track_path` | string | yes | Node path and property for the track (e.g. 'Sprite2D:position') |
| `track_type` | string | no | Track type: value, position_2d, rotation_2d, scale_2d, method, bezier, blend_shape (default: value) |
| `update_mode` | string | no | Update mode for value tracks: continuous, discrete, capture |

### set_animation_keyframe

Insert a keyframe into an animation track

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the AnimationPlayer node |
| `animation` | string | yes | Animation name |
| `track_index` | number | yes | Track index |
| `time` | number | yes | Time position in seconds |
| `value` | string \| number \| boolean | yes | Keyframe value. Strings auto-parsed for Vector2, Color, etc. |
| `easing` | number | no | Easing/transition value. 1.0=linear, <1.0=ease-in, >1.0=ease-out. Use negative for in-out variants. (default: 1.0) |

### get_animation_info

Get detailed info about an animation including all tracks and keyframes

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the AnimationPlayer node |
| `animation` | string | yes | Animation name |

### remove_animation

Remove an animation from an AnimationPlayer

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the AnimationPlayer node |
| `name` | string | yes | Name of the animation to remove |

## AnimationTree (9)

### create_animation_tree

Create an AnimationTree node with an AnimationNodeStateMachine as root, optionally linked to an AnimationPlayer

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the parent node where the AnimationTree will be added |
| `anim_player` | string | no | Relative path from the AnimationTree to the AnimationPlayer (e.g. '../AnimationPlayer') |
| `name` | string | no | Name for the AnimationTree node (default: 'AnimationTree') |

### get_animation_tree_structure

Read the full structure of an AnimationTree including all states, transitions, blend tree nodes, and blend space points (with point names on Godot 4.7+, sync_mode/cyclic_length on 4.7+, OneShot abort_on_reset on 4.6+)

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the AnimationTree node |

### add_state_machine_state

Add a state to an AnimationNodeStateMachine (animation clip, blend tree, nested state machine, or 1D/2D blend space with points)

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the AnimationTree node |
| `state_name` | string | yes | Name for the new state |
| `state_type` | "animation" \| "blend_tree" \| "state_machine" \| "blend_space_1d" \| "blend_space_2d" | no | Type of state: 'animation' (default), 'blend_tree', 'state_machine', 'blend_space_1d', or 'blend_space_2d' |
| `animation` | string | no | Animation name to play (only for state_type='animation') |
| `blend_points` | array | no | Blend space points (blend space types only; Godot allows at most 64) |
| `min_space` | number \| object \| array | no | Blend space minimum (number for 1D, {x,y} for 2D) |
| `max_space` | number \| object \| array | no | Blend space maximum (number for 1D, {x,y} for 2D) |
| `sync` | boolean | no | Blend space: keep non-dominant animations advancing (all versions) |
| `sync_mode` | "none" \| "independent" \| "cyclic_mutable" \| "cyclic_constant" | no | Blend space sync mode. 'cyclic_mutable'/'cyclic_constant' need Godot 4.7+; 'none'/'independent' map to the sync bool on older Godot |
| `cyclic_length` | number | no | Blend space cyclic sync length in seconds (Godot 4.7+) |
| `state_machine_path` | string | no | Slash-separated path to a nested state machine (e.g. 'Run/SubState'). Empty or omit for root. |
| `position_x` | number | no | X position in the graph editor (default: 0) |
| `position_y` | number | no | Y position in the graph editor (default: 0) |

### remove_state_machine_state

Remove a state from an AnimationNodeStateMachine (also removes connected transitions)

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the AnimationTree node |
| `state_name` | string | yes | Name of the state to remove |
| `state_machine_path` | string | no | Slash-separated path to a nested state machine. Empty or omit for root. |

### add_state_machine_transition

Add a transition between two states in an AnimationNodeStateMachine with configurable switch mode, advance mode, and expression conditions

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the AnimationTree node |
| `from_state` | string | yes | Source state name (use 'Start' for the entry point) |
| `to_state` | string | yes | Destination state name (use 'End' for the exit point) |
| `switch_mode` | "at_end" \| "immediate" \| "sync" | no | When to switch: 'at_end' (wait for animation), 'immediate' (default), 'sync' |
| `advance_mode` | "disabled" \| "enabled" \| "auto" | no | How to advance: 'disabled', 'enabled' (default, uses travel), 'auto' (automatic) |
| `advance_expression` | string | no | GDScript expression that triggers this transition (e.g. 'is_running') |
| `xfade_time` | number | no | Cross-fade time in seconds |
| `state_machine_path` | string | no | Slash-separated path to a nested state machine. Empty or omit for root. |

### remove_state_machine_transition

Remove a transition between two states in an AnimationNodeStateMachine

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the AnimationTree node |
| `from_state` | string | yes | Source state name |
| `to_state` | string | yes | Destination state name |
| `state_machine_path` | string | no | Slash-separated path to a nested state machine. Empty or omit for root. |

### set_blend_tree_node

Add or replace a node inside an AnimationNodeBlendTree state (Add2, Blend2, TimeScale, Animation, OneShot, BlendSpace1D/2D, etc.) with optional connection

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the AnimationTree node |
| `blend_tree_state` | string | yes | Name of the BlendTree state in the state machine |
| `bt_node_name` | string | yes | Name for the node inside the BlendTree |
| `bt_node_type` | "Animation" \| "Add2" \| "Blend2" \| "Add3" \| "Blend3" \| "TimeScale" \| "TimeSeek" \| "Transition" \| "OneShot" \| "Sub2" \| "BlendSpace1D" \| "BlendSpace2D" | yes | Type of BlendTree node to create |
| `animation` | string | no | Animation name (only for bt_node_type='Animation') |
| `abort_on_reset` | boolean | no | OneShot only (Godot 4.6+): abort the one-shot when the tree is reset |
| `blend_points` | array | no | Blend space points (blend space types only; Godot allows at most 64) |
| `min_space` | number \| object \| array | no | Blend space minimum (number for 1D, {x,y} for 2D) |
| `max_space` | number \| object \| array | no | Blend space maximum (number for 1D, {x,y} for 2D) |
| `sync` | boolean | no | Blend space: keep non-dominant animations advancing (all versions) |
| `sync_mode` | "none" \| "independent" \| "cyclic_mutable" \| "cyclic_constant" | no | Blend space sync mode. 'cyclic_mutable'/'cyclic_constant' need Godot 4.7+; 'none'/'independent' map to the sync bool on older Godot |
| `cyclic_length` | number | no | Blend space cyclic sync length in seconds (Godot 4.7+) |
| `connect_to` | string | no | Name of another BlendTree node to connect this node's output to |
| `connect_port` | number | no | Input port index on the target node (default: 0) |
| `state_machine_path` | string | no | Slash-separated path to a nested state machine. Empty or omit for root. |
| `position_x` | number | no | X position in the graph editor (default: 0) |
| `position_y` | number | no | Y position in the graph editor (default: 0) |

### set_tree_parameter

Set an AnimationTree parameter value (conditions, blend amounts, time scale, etc.)

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the AnimationTree node |
| `parameter` | string | yes | Parameter path (e.g. 'conditions/is_running', 'Blend2/blend_amount'). 'parameters/' prefix is auto-added if missing. |
| `value` | string \| number \| boolean | yes | Parameter value. Strings are auto-parsed for Vector2, Color, etc. |

### setup_ik_modifier

Add an IK SkeletonModifier3D (Godot 4.6+: TwoBoneIK3D, CCDIK3D, FABRIK3D, JacobianIK3D, SplineIK3D) as a child of a Skeleton3D and configure its chain(s): root/middle/end bones (validated against the skeleton and its hierarchy), target, pole or Path3D. One chain can be given with top-level fields, several with 'settings'. Undoable. On Godot 4.5 returns an error naming the running version.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `skeleton_path` | string | yes | Path to the Skeleton3D node |
| `ik_type` | "TwoBoneIK3D" \| "CCDIK3D" \| "FABRIK3D" \| "JacobianIK3D" \| "SplineIK3D" | no | IK modifier class (default: TwoBoneIK3D) |
| `name` | string | no | Node name (default: the ik_type) |
| `root_bone` | string | no | Root bone name of the chain (required) |
| `middle_bone` | string | no | TwoBoneIK3D only: middle bone (e.g. the elbow/knee), must descend from root_bone |
| `end_bone` | string | no | End bone name, must descend from root_bone (TwoBoneIK3D: from middle_bone). Optional for TwoBoneIK3D when use_virtual_end=true |
| `target_path` | string | no | Scene path of the Node3D the chain reaches for (TwoBoneIK3D, CCDIK3D, FABRIK3D, JacobianIK3D). Stored relative to the modifier |
| `pole_path` | string | no | TwoBoneIK3D only: scene path of the pole Node3D |
| `pole_direction` | "none" \| "+x" \| "-x" \| "+y" \| "-y" \| "+z" \| "-z" \| "custom" | no | TwoBoneIK3D only: pole direction axis |
| `pole_direction_vector` | object | no | TwoBoneIK3D only: custom pole direction (sets pole_direction='custom') |
| `path_3d` | string | no | SplineIK3D only (required): scene path of the Path3D the chain follows |
| `tilt_enabled` | boolean | no | SplineIK3D only: apply the curve tilt |
| `use_virtual_end` | boolean | no | TwoBoneIK3D only: use a virtual end bone extended from middle_bone |
| `extend_end_bone` | boolean | no | Extend the end bone by end_bone_length (auto-enabled when end_bone_length/direction is given) |
| `end_bone_length` | number | no | Virtual extension length in meters |
| `end_bone_direction` | "+x" \| "-x" \| "+y" \| "-y" \| "+z" \| "-z" \| "from_parent" | no | Direction of the end bone extension |
| `settings` | array | no | One entry per IK chain (setting_count = length). Overrides the top-level chain fields |
| `influence` | number | no | Modifier influence 0-1 (default: 1) |
| `active` | boolean | no | Whether the modifier is active (default: true) |
| `mutable_bone_axes` | boolean | no | Allow bone axes to change during solving |
| `max_iterations` | number | no | CCDIK3D/FABRIK3D/JacobianIK3D: max solver iterations |
| `min_distance` | number | no | CCDIK3D/FABRIK3D/JacobianIK3D: stop when the end is this close to the target |
| `angular_delta_limit` | number | no | CCDIK3D/FABRIK3D/JacobianIK3D: max rotation per iteration, in degrees |
| `deterministic` | boolean | no | CCDIK3D/FABRIK3D/JacobianIK3D: solve from the rest pose each frame |

## Audio (6)

### get_audio_bus_layout

Get the entire audio bus layout: all buses with volumes, effects, send targets, solo/mute states

No parameters.

### add_audio_bus

Add a new audio bus with name, volume, send target, solo, and mute settings

| Parameter | Type | Required | Description |
|---|---|---|---|
| `name` | string | yes | Name for the new audio bus |
| `volume_db` | number | no | Volume in dB (default: 0) |
| `send` | string | no | Name of the bus to send output to (e.g. 'Master') |
| `solo` | boolean | no | Solo this bus (default: false) |
| `mute` | boolean | no | Mute this bus (default: false) |
| `at_position` | number | no | Bus index position to insert at (-1 = end) |

### set_audio_bus

Modify an existing audio bus: volume, solo, mute, bypass_effects, send, or rename

| Parameter | Type | Required | Description |
|---|---|---|---|
| `name` | string | yes | Name of the audio bus to modify |
| `volume_db` | number | no | Volume in dB |
| `solo` | boolean | no | Solo state |
| `mute` | boolean | no | Mute state |
| `bypass_effects` | boolean | no | Bypass all effects on this bus |
| `send` | string | no | Name of the bus to send output to |
| `rename` | string | no | New name for the bus |

### add_audio_bus_effect

Add an audio effect to a bus. Types: reverb, chorus, delay, compressor, limiter, phaser, distortion, lowpassfilter, highpassfilter, bandpassfilter, amplify, eq

| Parameter | Type | Required | Description |
|---|---|---|---|
| `bus` | string | yes | Name of the audio bus |
| `effect_type` | string | yes | Effect type: reverb, chorus, delay, compressor, limiter, phaser, distortion, lowpassfilter (or lowpass), highpassfilter (or highpass), bandpassfilter (or bandpass), amplify, eq |
| `params` | object | no | Effect-specific parameters. E.g. for reverb: {room_size, damping, wet, dry, spread}; for compressor: {threshold, ratio, attack_us, release_ms}; for filters: {cutoff_hz, resonance} |
| `at_position` | number | no | Effect index position (-1 = end) |

### add_audio_player

Add an AudioStreamPlayer, AudioStreamPlayer2D, or AudioStreamPlayer3D node to a parent node

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the parent node |
| `name` | string | yes | Name for the new audio player node |
| `type` | string | no | Player type: AudioStreamPlayer (default), AudioStreamPlayer2D, AudioStreamPlayer3D |
| `stream` | string | no | Path to audio resource (e.g. 'res://audio/music.ogg') |
| `volume_db` | number | no | Volume in dB (default: 0) |
| `bus` | string | no | Audio bus name (default: 'Master') |
| `autoplay` | boolean | no | Auto-play when scene starts (default: false) |
| `max_distance` | number | no | Maximum hearing distance (for 2D/3D players) |
| `attenuation` | number | no | Distance attenuation factor (for 2D players) |
| `attenuation_model` | number | no | Attenuation model for 3D: 0=inverse_distance, 1=inverse_square, 2=logarithmic |
| `unit_size` | number | no | Unit size for 3D player volume reference |

### get_audio_info

Get audio setup for a node subtree: finds all AudioStreamPlayer nodes with their settings, streams, and bus assignments

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the root node to search within |

## Batch & Refactoring (7)

### find_nodes_by_type

Find all nodes of a specific type in the current scene

| Parameter | Type | Required | Description |
|---|---|---|---|
| `type` | string | yes | Node type/class to search for (e.g. 'Sprite2D', 'Label', 'CollisionShape2D') |
| `recursive` | boolean | no | Search recursively through children (default: true) |

### find_signal_connections

Find all signal connections in the current scene, optionally filtered by signal name or node

| Parameter | Type | Required | Description |
|---|---|---|---|
| `signal_name` | string | no | Filter by signal name (partial match) |
| `node_path` | string | no | Filter by node path (partial match) |

### batch_set_property

Set a property on all nodes of a given type in the current scene

| Parameter | Type | Required | Description |
|---|---|---|---|
| `type` | string | yes | Node type to target (e.g. 'Label', 'Sprite2D') |
| `property` | string | yes | Property name to set (e.g. 'visible', 'modulate') |
| `value` | string \| number \| boolean | yes | Value to set. Strings auto-parsed for Vector2, Color, etc. |

### batch_add_nodes

Add multiple nodes in a single call. Supports building entire node trees at once — nodes added earlier can be referenced as parents by later entries. Much faster than calling add_node repeatedly.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `nodes` | array | yes | Array of node definitions to add, processed in order |

### find_node_references

Search through project files (.tscn, .gd, .tres, .gdshader) for a text pattern

| Parameter | Type | Required | Description |
|---|---|---|---|
| `pattern` | string | yes | Text pattern to search for |

### get_scene_dependencies

Get all resource dependencies of a scene or resource file

| Parameter | Type | Required | Description |
|---|---|---|---|
| `path` | string | yes | Path to the scene or resource file (e.g. 'res://scenes/player.tscn') |

### cross_scene_set_property

Preview or apply a property change on all nodes of a given type across scene files in the project. Defaults to dry_run=true (returns the matching scenes and node paths without writing). To actually apply: pass force=true AND dry_run=false. Inactive open scenes are skipped and reported in skipped_open_scenes — open them as the active tab first to live-edit. The active open scene is live-edited via UndoRedo so changes are visible in the editor and undoable. Closed scenes are offline-saved. The response includes a per-scene `mode` field: dry_run / offline_saved / live_open_scene.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `type` | string | yes | Node type to target (e.g. 'Label', 'Sprite2D') |
| `property` | string | yes | Property name to set |
| `value` | string \| number \| boolean | yes | Value to set. Strings auto-parsed for Vector2, Color, etc. |
| `path_filter` | string | no | Directory to search in (default: 'res://') |
| `exclude_addons` | boolean | no | Exclude addons/ directory (default: true) |
| `dry_run` | boolean | no | Preview only — list affected scenes and nodes without writing. Defaults to true unless force=true is set. |
| `force` | boolean | no | Required (alongside dry_run=false) to actually write. Acknowledges that this can modify many scene files at once. |

## Export (4)

### list_export_presets

List all export presets configured in export_presets.cfg

No parameters.

### export_project

Get the export command for a preset (direct export from editor is not supported in Godot 4)

| Parameter | Type | Required | Description |
|---|---|---|---|
| `preset_name` | string | no | Export preset name |
| `preset_index` | number | no | Export preset index (alternative to name) |
| `debug` | boolean | no | Debug export (default: true) |

### export_patch_pck

Export a patch PCK that contains only the files changed since the given base packs (Godot's export_pack_patch, 4.4+). Runs Godot's own `--export-patch` CLI in a headless child process with the project's configured preset, waits for it, and reports the output size. Exports what is saved on disk (save scenes first) and always in release mode (the CLI has no debug variant). Blocks until done (default timeout 300s).

| Parameter | Type | Required | Description |
|---|---|---|---|
| `preset_name` | string | no | Export preset name (see list_export_presets) |
| `preset_index` | number | no | Export preset index (alternative to name) |
| `output_path` | string | yes | Where to write the patch, ending in .pck: res://, user:// or an absolute path. Its directory must already exist. Overwritten if present. |
| `patches` | array | no | Base .pck files the shipped game already has (res://, user:// or absolute paths; each must exist). Files identical to these are left out of the patch. If omitted, the preset's own Patches list is used. |
| `timeout_sec` | number | no | Give up after this many seconds (10-1800, default 300) |

### get_export_info

Get export-related project info (executable path, templates, project path)

No parameters.

## Navigation (5)

### setup_navigation_region

Add a NavigationRegion2D/3D child to a node with auto-created NavigationPolygon or NavigationMesh. Auto-detects 2D/3D from parent context.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the parent node to add the region to |
| `mode` | string | no | Force '2d' or '3d' mode, or 'auto' to detect from parent (default: auto) |
| `name` | string | no | Name for the NavigationRegion node |
| `navigation_layers` | number | no | Navigation layers bitmask |
| `agent_radius` | number | no | Agent radius for mesh generation (3D default: 0.5, 2D: from NavigationPolygon) |
| `agent_height` | number | no | Agent height (3D only, default: 1.5) |
| `agent_max_climb` | number | no | Max climb height (3D only, default: 0.25) |
| `agent_max_slope` | number | no | Max slope angle in degrees (3D only, default: 45.0) |
| `cell_size` | number | no | Cell size for navigation mesh (default: 0.25 for 3D) |
| `cell_height` | number | no | Cell height (3D only, default: 0.25) |
| `source_geometry_mode` | string | no | 2D only: root_node, groups_with_children, or groups_explicit |

### bake_navigation_mesh

Bake navigation mesh for a NavigationRegion3D, or set outline vertices and generate polygons for a NavigationRegion2D.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the NavigationRegion2D or NavigationRegion3D node |
| `outline` | array | no | 2D only: Array of outline vertices as [x,y] pairs or {x,y} objects. At least 3 vertices required. |

### setup_navigation_agent

Add a NavigationAgent2D/3D child to a node and configure pathfinding and avoidance properties. Auto-detects 2D/3D from parent context.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the parent node to add the agent to |
| `mode` | string | no | Force '2d' or '3d' mode, or 'auto' to detect from parent (default: auto) |
| `name` | string | no | Name for the NavigationAgent node |
| `path_desired_distance` | number | no | Distance threshold to advance to next path point |
| `target_desired_distance` | number | no | Distance threshold to consider target reached |
| `radius` | number | no | Agent radius for avoidance |
| `neighbor_distance` | number | no | Max distance to consider other agents as neighbors |
| `max_neighbors` | number | no | Max number of neighbors for avoidance |
| `max_speed` | number | no | Maximum movement speed for avoidance |
| `avoidance_enabled` | boolean | no | Enable avoidance behavior |
| `navigation_layers` | number | no | Navigation layers bitmask for pathfinding queries |

### set_navigation_layers

Set navigation layers for a NavigationRegion or NavigationAgent. Supports bitmask value, layer bit numbers, or named layers from ProjectSettings.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to a NavigationRegion2D/3D or NavigationAgent2D/3D node |
| `layers` | number | no | Navigation layers as a bitmask value (e.g. 5 = layers 1 and 3) |
| `layer_bits` | array | no | Array of 1-based layer numbers to enable (e.g. [1, 3] = bitmask 5) |
| `layer_names` | array | no | Array of named layer names from ProjectSettings (layer_names/2d_navigation/layer_N or 3d) |

### get_navigation_info

Get navigation setup info for a node and its subtree: all NavigationRegions, NavigationAgents, their layers, and mesh/polygon data.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the root node to inspect |

## Particles (5)

### create_particles

Add a GPUParticles2D or GPUParticles3D node with a ParticleProcessMaterial. Configure amount, lifetime, one_shot, explosiveness, and randomness.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `parent_path` | string | yes | Path to the parent node to add particles to |
| `name` | string | no | Name for the particles node (default: 'Particles') |
| `is_3d` | boolean | no | Create GPUParticles3D instead of GPUParticles2D (default: false) |
| `amount` | number | no | Number of particles (default: 16) |
| `lifetime` | number | no | Particle lifetime in seconds (default: 1.0) |
| `one_shot` | boolean | no | Emit only once (default: false) |
| `explosiveness` | number | no | Explosiveness ratio 0-1 (default: 0.0) |
| `randomness` | number | no | Randomness ratio 0-1 (default: 0.0) |
| `emitting` | boolean | no | Start emitting immediately (default: true) |

### set_particle_material

Configure ParticleProcessMaterial properties: direction, spread, velocity, gravity, scale, color, emission shape (point/sphere/box/ring), angular/orbit velocity, damping, and attractor interaction. Godot 4.7+ adds per-axis 3D scale/rotation (scale_3d_*, rotation_3d_*, rotation_velocity_3d_*) and inherit_emitter_scale; on older Godot these return an error naming the running version. Only GPUParticles2D/3D are handled (CPUParticles2D's 4.6 ring emission shape, emission_ring_radius/emission_ring_inner_radius, can be set with update_property).

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the GPUParticles2D/3D node |
| `direction` | object | no | Emission direction vector |
| `spread` | number | no | Spread angle in degrees (0-180) |
| `initial_velocity_min` | number | no | Minimum initial velocity |
| `initial_velocity_max` | number | no | Maximum initial velocity |
| `gravity` | object | no | Gravity vector |
| `scale_min` | number | no | Minimum particle scale |
| `scale_max` | number | no | Maximum particle scale |
| `color` | string | no | Particle color (hex '#RRGGBB' or named color) |
| `emission_shape` | string | no | Emission shape: point, sphere, sphere_surface, box, ring |
| `emission_sphere_radius` | number | no | Sphere emission radius |
| `emission_box_extents` | object | no | Box emission extents |
| `emission_ring_radius` | number | no | Ring outer radius |
| `emission_ring_inner_radius` | number | no | Ring inner radius |
| `emission_ring_height` | number | no | Ring height |
| `angular_velocity_min` | number | no | Minimum angular velocity (degrees/sec) |
| `angular_velocity_max` | number | no | Maximum angular velocity (degrees/sec) |
| `orbit_velocity_min` | number | no | Minimum orbit velocity |
| `orbit_velocity_max` | number | no | Maximum orbit velocity |
| `damping_min` | number | no | Minimum damping |
| `damping_max` | number | no | Maximum damping |
| `attractor_interaction_enabled` | boolean | no | Enable attractor interaction |
| `inherit_emitter_scale` | boolean | no | Godot 4.7+: particles inherit the emitter node's scale (particle_flag_inherit_emitter_scale) |
| `use_scale_3d` | boolean | no | Godot 4.7+: use per-axis scale_3d_min/max instead of scale_min/max (auto-enabled when scale_3d_* is given) |
| `scale_3d_min` | object \| array \| number | no | Godot 4.7+: minimum per-axis scale as {x,y,z}, [x,y,z] or a uniform number |
| `scale_3d_max` | object \| array \| number | no | Godot 4.7+: maximum per-axis scale |
| `use_rotation_3d` | boolean | no | Godot 4.7+: use per-axis initial rotation (auto-enabled when rotation_3d_* is given) |
| `rotation_3d_min` | object \| array \| number | no | Godot 4.7+: minimum per-axis initial rotation (same units as the inspector's Rotation 3D fields) |
| `rotation_3d_max` | object \| array \| number | no | Godot 4.7+: maximum per-axis initial rotation |
| `use_rotation_velocity_3d` | boolean | no | Godot 4.7+: use per-axis rotation velocity (auto-enabled when rotation_velocity_3d_* is given) |
| `rotation_velocity_3d_min` | object \| array \| number | no | Godot 4.7+: minimum per-axis rotation velocity |
| `rotation_velocity_3d_max` | object \| array \| number | no | Godot 4.7+: maximum per-axis rotation velocity |

### set_particle_color_gradient

Set a color ramp (gradient) on a particle system's material. Provide an array of color stops with offset (0-1) and color.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the GPUParticles2D/3D node |
| `stops` | array | yes | Array of gradient color stops |

### apply_particle_preset

Apply a named particle preset. Available presets: explosion (burst, short life), fire (upward, orange gradient), smoke (slow upward, gray), sparks (burst, high velocity), rain (downward, blue), snow (slow downward, drift), magic (orbit, colorful), dust (ambient, subtle).

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the GPUParticles2D/3D node |
| `preset` | string | yes | Preset name: explosion, fire, smoke, sparks, rain, snow, magic, dust |

### get_particle_info

Get the full configuration of a particle system: node properties, material settings, emission shape, color gradient stops.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the GPUParticles2D/3D node |

## Physics (6)

### setup_collision

Add a CollisionShape2D/3D child to a physics body or area node with a specified shape. Auto-detects 2D/3D from the parent node type.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the parent physics body or area node (e.g. CharacterBody2D, StaticBody3D, Area2D) |
| `shape` | string | yes | Shape type: 'rectangle'/'rect', 'circle', 'capsule', 'segment' (2D only), 'cylinder' (3D only), 'custom'/'convex'. For 3D: 'box'/'sphere' also work. |
| `width` | number | no | Width for rectangle/box shape (default: 32 for 2D, 1 for 3D) |
| `height` | number | no | Height for rectangle/box/capsule/cylinder shape |
| `depth` | number | no | Depth for 3D box shape (default: 1) |
| `radius` | number | no | Radius for circle/sphere/capsule/cylinder shape |
| `ax` | number | no | Segment start X (2D segment only) |
| `ay` | number | no | Segment start Y (2D segment only) |
| `bx` | number | no | Segment end X (2D segment only) |
| `by` | number | no | Segment end Y (2D segment only) |
| `points` | array | no | Convex polygon points as [[x,y],...] for 2D or [[x,y,z],...] for 3D |
| `disabled` | boolean | no | Create the collision shape disabled (default: false) |
| `one_way_collision` | boolean | no | Enable one-way collision (2D only, default: false) |
| `dimension` | string | no | Force '2d' or '3d' if auto-detection fails |

### set_physics_layers

Set collision layer and/or mask on a physics body or area node. Supports bitmask integers or arrays of layer numbers.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the node with collision layers |
| `collision_layer` | number \| array | no | Collision layer: bitmask integer or array of layer numbers [1,3,5] |
| `collision_mask` | number \| array | no | Collision mask: bitmask integer or array of layer numbers [1,2,4] |

### get_physics_layers

Get the current collision layer and mask for a node, including named layer info from ProjectSettings.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the node with collision layers |

### add_raycast

Add a RayCast2D/3D child node for collision detection. Auto-detects 2D/3D from the parent node type.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the parent node |
| `name` | string | no | Name for the raycast node (default: 'RayCast') |
| `target_x` | number | no | Target position X (default: 0) |
| `target_y` | number | no | Target position Y (default: 50 for 2D, -1 for 3D) |
| `target_z` | number | no | Target position Z (3D only, default: 0) |
| `collision_mask` | number | no | Collision mask bitmask (default: 1) |
| `enabled` | boolean | no | Enable the raycast (default: true) |
| `collide_with_areas` | boolean | no | Collide with Area nodes (default: false) |
| `collide_with_bodies` | boolean | no | Collide with physics bodies (default: true) |
| `hit_from_inside` | boolean | no | Detect hits from inside shapes (default: false) |
| `dimension` | string | no | Force '2d' or '3d' if auto-detection fails |

### setup_physics_body

Configure physics body properties. For CharacterBody2D/3D: floor settings, motion mode, etc. For RigidBody2D/3D: mass, gravity, damping, etc.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the physics body node |
| `floor_stop_on_slope` | boolean | no | CharacterBody: stop on slopes when not moving |
| `floor_max_angle` | number | no | CharacterBody: maximum floor angle in radians (default ~0.785 = 45 degrees) |
| `floor_snap_length` | number | no | CharacterBody: floor snap distance for sticking to the ground |
| `wall_min_slide_angle` | number | no | CharacterBody: minimum angle for wall sliding in radians |
| `motion_mode` | string | no | CharacterBody: 'grounded' or 'floating' |
| `max_slides` | number | no | CharacterBody: maximum slide iterations (default: 6) |
| `slide_on_ceiling` | boolean | no | CharacterBody: allow sliding on ceiling |
| `mass` | number | no | RigidBody: mass in kg (default: 1) |
| `gravity_scale` | number | no | RigidBody: gravity multiplier (default: 1, 0 = no gravity) |
| `linear_damp` | number | no | RigidBody: linear velocity damping |
| `angular_damp` | number | no | RigidBody: angular velocity damping |
| `freeze` | boolean | no | RigidBody: freeze the body (stop physics simulation) |
| `freeze_mode` | string | no | RigidBody: 'static' or 'kinematic' freeze behavior |
| `continuous_cd` | string \| boolean | no | RigidBody: continuous collision detection. 2D: 'disabled'/'cast_ray'/'cast_shape'. 3D: true/false |
| `contact_monitor` | boolean | no | RigidBody: enable contact monitoring for body_entered/body_exited signals |
| `max_contacts_reported` | number | no | RigidBody: max contacts to report (requires contact_monitor) |

### get_collision_info

Get detailed collision information for a node: all collision shapes, layers/masks, raycasts, physics body settings, and the project's physics engine (physics_engine: raw physics/3d and physics/2d settings plus the effective engine; DEFAULT resolves to Godot Physics on 4.5-4.7, while projects created in the 4.6+ editor store 'Jolt Physics' explicitly). Scans children by default.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the node to inspect |
| `include_children` | boolean | no | Include children in the scan (default: true) |

## Profiling (2)

### get_performance_monitors

Get the RUNNING GAME's performance monitors (FPS, memory, draw calls, physics, navigation, etc.). Requires a scene to be playing (play_scene). For editor-process metrics use get_editor_performance. IMPORTANT: an embedded game is throttled to ~10 FPS while the editor is unfocused — always the case when driven from a terminal — so time/fps and time/process are then meaningless. The response reports fps_throttled (true / false / null when the game runs in a floating Game workspace whose focus cannot be read), plus editor_focused and game_embedded. Draw-call, primitive and memory counters are never affected.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `category` | string | no | Filter by category prefix: 'fps', 'memory', 'render', 'physics_2d', 'physics_3d', 'navigation' |

### get_editor_performance

Get a quick performance summary (FPS, frame time, draw calls, memory usage)

No parameters.

## Resource (4)

### read_resource

Read a .tres resource file and return its properties. Works with any Godot Resource type (StyleBox, Font, Theme, Material, etc.)

| Parameter | Type | Required | Description |
|---|---|---|---|
| `path` | string | yes | Path to the resource file (e.g. 'res://themes/main_theme.tres') |

### edit_resource

Edit properties of an existing .tres resource file. Changes are saved to disk immediately.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `path` | string | yes | Path to the resource file (e.g. 'res://themes/main_theme.tres') |
| `properties` | object | yes | Properties to set as key-value pairs. Values auto-parsed for Vector2, Color, etc. |

### create_resource

Create a new .tres resource file of a given type with optional initial properties

| Parameter | Type | Required | Description |
|---|---|---|---|
| `path` | string | yes | Path to save the resource (e.g. 'res://resources/player_stats.tres') |
| `type` | string | yes | Resource type to create (e.g. 'StyleBoxFlat', 'LabelSettings', 'Environment') |
| `properties` | object | no | Initial properties to set |
| `overwrite` | boolean | no | Overwrite if file exists (default: false) |

### get_resource_preview

Get a visual preview of an image or texture resource as a PNG. Works with .png, .jpg, .webp, .svg image files and Texture2D resources.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `path` | string | yes | Path to the resource (e.g. 'res://assets/player.png', 'res://icon.svg') |
| `max_size` | number | no | Maximum width/height in pixels, preserving aspect ratio (default: 256) |

## 3D Scene (7)

### add_mesh_instance

Add a MeshInstance3D node with a primitive mesh (Box, Sphere, Cylinder, Capsule, Plane, Prism, Torus, Quad) or load a 3D model file (.glb/.gltf/.obj). Set position, rotation, scale, and mesh-specific properties.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `mesh_type` | string | no | Primitive mesh type: BoxMesh, SphereMesh, CylinderMesh, CapsuleMesh, PlaneMesh, PrismMesh, TorusMesh, QuadMesh |
| `mesh_file` | string | no | Path to a 3D model file (res://path/to/model.glb, .gltf, .obj). Use instead of mesh_type for imported models |
| `parent_path` | string | no | Parent node path (default: root '.') |
| `name` | string | no | Node name (default: MeshInstance3D) |
| `position` | any | no | Position as Vector3 string 'Vector3(x,y,z)', object {x,y,z}, or array [x,y,z] |
| `rotation` | any | no | Rotation in degrees as Vector3 string, object {x,y,z}, or array [x,y,z] |
| `scale` | any | no | Scale as Vector3 string, object {x,y,z}, or array [x,y,z] |
| `mesh_properties` | object | no | Properties to set on the mesh resource (e.g. {"size": "Vector3(2,1,2)"} for BoxMesh) |

### setup_lighting

Add a light node (DirectionalLight3D, OmniLight3D, SpotLight3D, or AreaLight3D on Godot 4.7+) to the scene. Supports preset configurations: 'sun' (directional with shadows), 'indoor' (warm omni), 'dramatic' (focused spot with shadows).

| Parameter | Type | Required | Description |
|---|---|---|---|
| `light_type` | string | no | Light type: DirectionalLight3D, OmniLight3D, SpotLight3D, AreaLight3D (Godot 4.7+; returns an error naming the running version on older Godot). Not needed if preset is specified |
| `preset` | string | no | Preset configuration: 'sun' (directional, shadows, -45deg), 'indoor' (warm omni, range 8), 'dramatic' (spot, high energy, shadows) |
| `parent_path` | string | no | Parent node path (default: root '.') |
| `name` | string | no | Node name |
| `color` | any | no | Light color as Color string or hex (default: white) |
| `energy` | number | no | Light energy/intensity (default: 1.0) |
| `shadows` | boolean | no | Enable shadow casting (default: false, true for sun/dramatic presets) |
| `range` | number | no | Range for OmniLight3D/SpotLight3D (default: 5.0); alias for area_range on AreaLight3D |
| `attenuation` | number | no | Attenuation for OmniLight3D/SpotLight3D (default: 1.0); alias for area_attenuation on AreaLight3D |
| `spot_angle` | number | no | Spot angle in degrees for SpotLight3D (default: 45.0) |
| `spot_angle_attenuation` | number | no | Spot angle attenuation for SpotLight3D (default: 1.0) |
| `area_size` | object \| array \| string | no | AreaLight3D only (Godot 4.7+): rectangle size in meters as {x,y}, [x,y] or 'Vector2(x, y)' |
| `area_range` | number | no | AreaLight3D only (Godot 4.7+): light range in meters |
| `area_attenuation` | number | no | AreaLight3D only (Godot 4.7+): distance attenuation curve |
| `area_normalize_energy` | boolean | no | AreaLight3D only (Godot 4.7+): keep total emitted energy constant regardless of area_size |
| `area_texture` | string | no | AreaLight3D only (Godot 4.7+): res:// path to a Texture2D that colors the emitting area |
| `position` | any | no | Position as Vector3 string, object {x,y,z}, or array [x,y,z] |
| `rotation` | any | no | Rotation in degrees as Vector3 string, object {x,y,z}, or array [x,y,z] |

### set_material_3d

Create and apply a StandardMaterial3D to a MeshInstance3D. Configure PBR properties: albedo color/texture, metallic, roughness, emission, transparency, normal maps.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the MeshInstance3D node |
| `surface_index` | number | no | Surface index to apply material to (default: 0) |
| `albedo_color` | any | no | Albedo color as Color string 'Color(r,g,b,a)', hex '#ff0000', or object {r,g,b,a} |
| `albedo_texture` | string | no | Path to albedo texture (res://path/to/texture.png) |
| `metallic` | number | no | Metallic value 0.0-1.0 (default: 0.0) |
| `roughness` | number | no | Roughness value 0.0-1.0 (default: 1.0) |
| `metallic_texture` | string | no | Path to metallic texture |
| `roughness_texture` | string | no | Path to roughness texture |
| `normal_texture` | string | no | Path to normal map texture (auto-enables normal mapping) |
| `emission` | any | no | Emission color (auto-enables emission). Color string or hex |
| `emission_color` | any | no | Alias for emission |
| `emission_energy` | number | no | Emission energy multiplier (default: 1.0) |
| `emission_texture` | string | no | Path to emission texture |
| `transparency` | string | no | Transparency mode: DISABLED, ALPHA, ALPHA_SCISSOR, ALPHA_HASH, ALPHA_DEPTH_PRE_PASS |
| `cull_mode` | string | no | Cull mode: BACK, FRONT, DISABLED |

### setup_environment

Add or configure a WorldEnvironment node with sky, ambient light, tonemap, fog, glow, SSAO, SSR, and SDFGI settings.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `parent_path` | string | no | Parent node path (default: root '.') |
| `node_path` | string | no | Path to an existing WorldEnvironment to modify instead of creating a new one |
| `name` | string | no | Node name (default: WorldEnvironment) |
| `background_mode` | string | no | Background mode: 'sky', 'color', 'canvas', 'clear_color' (default: sky) |
| `background_color` | any | no | Background color when mode is 'color' |
| `sky` | object | no | ProceduralSkyMaterial settings |
| `ambient_light_color` | any | no | Ambient light color |
| `ambient_light_energy` | number | no | Ambient light energy |
| `ambient_light_source` | string | no | Ambient light source: BACKGROUND, DISABLED, COLOR, SKY |
| `tonemap_mode` | string | no | Tonemap mode: LINEAR, REINHARDT, FILMIC, ACES |
| `tonemap_exposure` | number | no | Tonemap exposure |
| `tonemap_white` | number | no | Tonemap white point |
| `fog_enabled` | boolean | no | Enable volumetric fog |
| `fog_light_color` | any | no | Fog light color |
| `fog_density` | number | no | Fog density |
| `fog_light_energy` | number | no | Fog light energy |
| `glow_enabled` | boolean | no | Enable glow/bloom |
| `glow_intensity` | number | no | Glow intensity |
| `glow_strength` | number | no | Glow strength |
| `glow_bloom` | number | no | Glow bloom amount |
| `ssao_enabled` | boolean | no | Enable Screen-Space Ambient Occlusion |
| `ssao_radius` | number | no | SSAO radius |
| `ssao_intensity` | number | no | SSAO intensity |
| `ssr_enabled` | boolean | no | Enable Screen-Space Reflections |
| `ssr_max_steps` | number | no | SSR max steps |
| `ssr_fade_in` | number | no | SSR fade in |
| `ssr_fade_out` | number | no | SSR fade out |
| `sdfgi_enabled` | boolean | no | Enable Signed Distance Field Global Illumination |

### setup_camera_3d

Add or configure a Camera3D node. Set projection mode, FOV, near/far planes, position, rotation, look-at target, and cull mask.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `parent_path` | string | no | Parent node path (default: root '.') |
| `node_path` | string | no | Path to an existing Camera3D to configure instead of creating a new one |
| `name` | string | no | Node name (default: Camera3D) |
| `projection` | string | no | Projection mode: 'perspective', 'orthogonal'/'orthographic', 'frustum' |
| `fov` | number | no | Field of view in degrees for perspective (default: 75) |
| `size` | number | no | View size for orthogonal projection |
| `near` | number | no | Near clipping plane (default: 0.05) |
| `far` | number | no | Far clipping plane (default: 4000) |
| `cull_mask` | number | no | Cull mask as integer bitmask |
| `current` | boolean | no | Make this the current/active camera (default: false) |
| `position` | any | no | Position as Vector3 (default: (0, 1, 3) for new cameras) |
| `rotation` | any | no | Rotation in degrees as Vector3 |
| `look_at` | any | no | Target position to look at as Vector3 (overrides rotation) |
| `environment_path` | string | no | Path to an Environment resource for camera-specific environment override |

### add_gridmap

Add or configure a GridMap node with a MeshLibrary. Optionally set cells at specific grid positions with item IDs and orientations.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `parent_path` | string | no | Parent node path (default: root '.') |
| `node_path` | string | no | Path to an existing GridMap to configure instead of creating a new one |
| `name` | string | no | Node name (default: GridMap) |
| `mesh_library_path` | string | no | Path to a MeshLibrary resource (res://path/to/library.meshlib or .tres) |
| `cell_size` | any | no | Cell size as Vector3 (default: (2, 2, 2)) |
| `position` | any | no | GridMap position as Vector3 |
| `cells` | array | no | Array of cells to set with grid positions and item IDs |

### get_gridmap_info

Inspect a GridMap: MeshLibrary path, cell size, used-cell count, bounds of used cells, per-item counts, and (Godot 4.7+) an octant summary. Optionally list cells filtered by item and/or cell-space bounds, capped by max_cells (truncation is always reported). On Godot 4.5/4.6 the octant block reports available=false.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the GridMap node |
| `item` | number | no | Only list/count cells with this MeshLibrary item ID (implies list_cells=true) |
| `bounds` | object | no | Cell-coordinate box to list cells from: {min,max} (both corners inclusive) or {position,size} (size cells starting at position, like an AABB; size 1 = one cell). Implies list_cells=true |
| `list_cells` | boolean | no | Include a 'cells' array (default: true when item or bounds is given, else false) |
| `max_cells` | number | no | Maximum cells returned in 'cells' (default: 500). 'cells_truncated' tells whether more matched |
| `include_octants` | boolean | no | Include the octant summary on Godot 4.7+ (default: true) |
| `max_octants` | number | no | Maximum octants listed (default: 200) |

## Shader (6)

### create_shader

Create a new shader file with a template or custom content. Refuses to overwrite a shader that is currently loaded/open in the editor unless force=true.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `path` | string | yes | Path for the shader file (e.g. 'res://shaders/dissolve.gdshader') |
| `shader_type` | string | no | Shader type: spatial, canvas_item, particles, sky (default: spatial) |
| `content` | string | no | Full shader code. If empty, generates a template. |
| `force` | boolean | no | Override the open/cached-shader guard and write anyway. |

### read_shader

Read the content of a shader file

| Parameter | Type | Required | Description |
|---|---|---|---|
| `path` | string | yes | Path to the shader file |

### edit_shader

Edit a shader file using full replacement or search-and-replace. Refreshes any cached/loaded copy of the shader via take_over_path + emit_changed so live materials pick up changes. Refuses to write if the shader is currently loaded/open in the editor unless force=true.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `path` | string | yes | Path to the shader file |
| `content` | string | no | Full replacement content |
| `replacements` | array | no | Array of search-and-replace operations |
| `force` | boolean | no | Override the open/cached-shader guard and write anyway. |

### assign_shader_material

Create a ShaderMaterial from a shader file and assign it to a node

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the target node (CanvasItem or MeshInstance3D) |
| `shader_path` | string | yes | Path to the shader file |

### set_shader_param

Set a shader parameter on a node's ShaderMaterial

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the node with a ShaderMaterial |
| `param` | string | yes | Shader parameter name |
| `value` | string \| number \| boolean | yes | Parameter value. Strings auto-parsed for Vector2, Color, etc. |

### get_shader_params

Get all shader parameters and their current values from a node's ShaderMaterial

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the node with a ShaderMaterial |

## Testing & QA (6)

### run_test_scenario

Execute a test scenario in the running game. Optionally plays a scene, then runs a sequence of steps (input simulation, waits, assertions, screenshots). Returns pass/fail summary. Note: keep scenarios short (under 20 seconds total) to avoid timeout.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `scene_path` | string | no | Scene to play before running steps. Use 'main' for main scene, 'current' for current scene, or a res:// path. If omitted, uses already-running scene. |
| `steps` | array | yes | Array of test steps. Each step has a 'type' field: 'input' (action/keycode simulation), 'wait' (seconds or node_path to wait for), 'assert' (node_path+property+expected+operator, or text for screen text), 'screenshot' (capture a frame). Examples: {type:'input', action:'ui_accept'}, {type:'wait', seconds:0.5}, {type:'wait', node_path:'/root/Main/Player'}, {type:'assert', node_path:'/root/Main/Player', property:'health', expected:100, operator:'eq'}, {type:'assert', text:'Game Over'}, {type:'screenshot'} |

### assert_node_state

Assert a node's property value in the running game. Compares the actual property value against an expected value using the specified operator. Returns pass/fail with actual value for debugging.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Absolute node path in the running game (e.g. '/root/Main/Player') |
| `property` | string | yes | Property name to check (e.g. 'health', 'position:x', 'visible') |
| `expected` | string \| number \| boolean | yes | Expected value to compare against |
| `operator` | "eq" \| "neq" \| "gt" \| "lt" \| "gte" \| "lte" \| "contains" \| "type_is" | no | Comparison operator (default: 'eq'). eq/neq: equality, gt/lt/gte/lte: numeric comparison, contains: string/array contains, type_is: check Godot type name |

### assert_screen_text

Assert that specific text is visible on screen in the running game. Searches all visible UI elements (Button, Label, LineEdit, etc.) for matching text. Useful for verifying UI state, dialog content, or game messages.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `text` | string | yes | Text to search for on screen (e.g. 'Game Over', 'Score:', 'New Game') |
| `partial` | boolean | no | Allow partial text matching (default: true). If false, requires exact match. |
| `case_sensitive` | boolean | no | Case-sensitive comparison (default: true) |

### run_stress_test

Run rapid random input events for a specified duration and check if the game crashes. Sends random UI actions (up/down/left/right/accept/cancel) plus any custom actions. Returns whether the game survived, event count, and new errors.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `duration` | number | no | Duration in seconds to run the stress test (1-60, default: 5) |
| `actions` | array | no | Additional input action names to include in random input pool (e.g. ['jump', 'attack', 'dash']) |

### set_game_speed

Read or set the running game's speed (Engine.time_scale). Requires a scene to be playing (play_scene). Omit 'scale' to only read the current value. Scales _process/_physics_process delta, timers, tweens and animations, so slow motion (e.g. 0.25) helps inspect fast effects with capture_frames and fast-forward (e.g. 4) shortens waits in tests. Only time_scale is changed: physics_ticks_per_second is left alone because changing it alters the simulation itself; both are reported, together with max_physics_steps_per_frame, which caps how far physics can keep up at high scales. The value lasts until the game stops. Values outside 0.05-20 are clamped (the response says so). MCP waits such as run_test_scenario 'wait' steps use real time and are not scaled.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `scale` | number | no | New Engine.time_scale (1 = normal, 0.5 = half speed, 2 = double). Clamped to 0.05-20. Omit to read the current speed without changing it. |

### get_test_report

Collect and format results from all assertions run so far (via assert_node_state, assert_screen_text, and run_test_scenario) into a summary test report. Shows pass/fail counts, pass rate, and detailed results.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `clear` | boolean | no | Clear accumulated results after generating report (default: true) |

## Theme & UI (8)

### create_theme

Create a new Theme resource file

| Parameter | Type | Required | Description |
|---|---|---|---|
| `path` | string | yes | Path to save the theme (e.g. 'res://themes/main.tres') |
| `default_font_size` | number | no | Default font size |

### set_theme_color

Set a theme color override on a Control node

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the Control node |
| `name` | string | yes | Color name (e.g. 'font_color', 'font_hover_color') |
| `color` | string | yes | Color as hex string (e.g. '#ff0000') or name |
| `theme_type` | string | no | Theme type (defaults to node's class) |

### set_theme_constant

Set a theme constant override on a Control node

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the Control node |
| `name` | string | yes | Constant name (e.g. 'margin_left', 'separation') |
| `value` | number | yes | Integer value |

### set_theme_font_size

Set a theme font size override on a Control node

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the Control node |
| `name` | string | yes | Font size name (e.g. 'font_size') |
| `size` | number | yes | Font size in pixels |

### set_theme_stylebox

Set a StyleBoxFlat override on a Control node with customizable appearance

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the Control node |
| `name` | string | yes | Style name (e.g. 'panel', 'normal', 'hover', 'pressed') |
| `bg_color` | string | no | Background color (hex) |
| `border_color` | string | no | Border color (hex) |
| `border_width` | number | no | Border width in pixels |
| `corner_radius` | number | no | Corner radius in pixels |
| `padding` | number | no | Content padding in pixels |

### setup_control

Configure a Control/Container node's layout properties in one call. Sets anchor preset, margins, min size, size flags, and container-specific properties like separation — much faster than multiple update_property calls. Newer Control properties are only set when passed: pivot_offset_ratio (Godot 4.6+), max_size and offset_transform (Godot 4.7+); on older versions passing them returns an error. Undoable as one action.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the Control node |
| `anchor_preset` | string | no | Anchor preset: 'top_left', 'top_right', 'bottom_left', 'bottom_right', 'center_left', 'center_top', 'center_right', 'center_bottom', 'center', 'left_wide', 'top_wide', 'right_wide', 'bottom_wide', 'vcenter_wide', 'hcenter_wide', 'full_rect' |
| `min_size` | string | no | Minimum size as 'Vector2(w, h)' |
| `size_flags_h` | string | no | Horizontal size flags: 'fill', 'expand', 'fill_expand', 'shrink_center', 'shrink_end' |
| `size_flags_v` | string | no | Vertical size flags: 'fill', 'expand', 'fill_expand', 'shrink_center', 'shrink_end' |
| `margins` | object | no | Margin overrides for MarginContainer (sets theme constants margin_left/right/top/bottom) |
| `separation` | number | no | Separation for VBoxContainer/HBoxContainer (theme constant override) |
| `grow_h` | string | no | Horizontal grow direction: 'begin', 'end', 'both' |
| `grow_v` | string | no | Vertical grow direction: 'begin', 'end', 'both' |
| `pivot_offset_ratio` | string \| object \| array | no | Godot 4.6+. Pivot for rotation/scale as a fraction of the size, e.g. 'Vector2(0.5, 0.5)' = centre. Added to pivot_offset (pixels); unlike it, follows resizes. |
| `max_size` | string \| object \| array | no | Godot 4.7+. Sets custom_maximum_size, e.g. 'Vector2(400, 300)'. A negative axis (the default -1) means no maximum. |
| `offset_transform` | object | no | Godot 4.7+. Control offset transform (offset_transform_* properties): moves/rotates/scales a Control after layout without fighting its container — handy for hover/press animations inside containers. |

### add_virtual_joystick

Add a VirtualJoystick (Godot 4.7+ on-screen touch joystick) that presses the given input actions. Returns an error on older versions. Validates enum values and ranges; warns (does not fail) when an action is missing from the project's Input Map. The node's rect is its touch area, so a size is always set (default: 2.5x joystick_size, square). It reacts to touch only: enable input_devices/pointing/emulate_touch_from_mouse to test it with a mouse. Appearance comes from the theme styleboxes normal_joystick, normal_tip, pressed_joystick and pressed_tip (set_theme_stylebox). Undoable.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `parent_path` | string | no | Parent node path (default: scene root '.'). Usually a CanvasLayer or full-rect Control for HUDs. |
| `name` | string | no | Node name (default: 'VirtualJoystick') |
| `action_left` | string | no | Input action pressed when pushed left (default: 'ui_left') |
| `action_right` | string | no | Input action pressed when pushed right (default: 'ui_right') |
| `action_up` | string | no | Input action pressed when pushed up (default: 'ui_up') |
| `action_down` | string | no | Input action pressed when pushed down (default: 'ui_down') |
| `mode` | "fixed" \| "dynamic" \| "following" | no | fixed: stays where placed; dynamic: appears where the touch starts; following: moves with a finger dragged past its edge (default: fixed) |
| `visibility` | "always" \| "when_touched" | no | Default: always |
| `joystick_size` | number | no | Base circle diameter in px, 10-500 (default 100) |
| `tip_size` | number | no | Tip diameter in px, 5-250 (default 50) |
| `deadzone_ratio` | number | no | 0-1 fraction of the radius ignored (default 0) |
| `clampzone_ratio` | number | no | 0-2, how far the tip may travel relative to the radius (default 1) |
| `initial_offset_ratio` | string \| object \| array | no | Where the stick rests inside the node's rect, as a fraction (default 'Vector2(0.5, 0.5)') |
| `size` | string \| object \| array | no | Touch-area size in px, e.g. 'Vector2(300, 300)' |
| `anchor_preset` | string | no | Anchor preset keeping the size, e.g. 'bottom_left' or 'bottom_right' (same names as setup_control). Exclusive with position. |
| `margin` | number | no | Distance from the anchored edges in px when anchor_preset is set (default 0) |
| `position` | string \| object \| array | no | Position in px when no anchor_preset is given |

### get_theme_info

Get theme information and overrides for a Control node

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the Control node |

## TileMap (6)

### tilemap_set_cell

Set a single cell in a TileMapLayer (or a deprecated multi-layer TileMap node)

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the TileMapLayer or deprecated TileMap node |
| `x` | number | yes | Cell X coordinate |
| `y` | number | yes | Cell Y coordinate |
| `source_id` | number | no | Tile source ID (default: 0) |
| `atlas_x` | number | no | Atlas X coordinate (default: 0) |
| `atlas_y` | number | no | Atlas Y coordinate (default: 0) |
| `alternative` | number | no | Alternative tile ID (default: 0) |
| `layer` | number | no | Layer index for deprecated multi-layer TileMap nodes (default: 0; TileMapLayer has one implicit layer and only accepts 0) |

### tilemap_fill_rect

Fill a rectangular region of a TileMapLayer (or a deprecated multi-layer TileMap node) with tiles

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the TileMapLayer or deprecated TileMap node |
| `x1` | number | yes | Start X coordinate |
| `y1` | number | yes | Start Y coordinate |
| `x2` | number | yes | End X coordinate |
| `y2` | number | yes | End Y coordinate |
| `source_id` | number | no | Tile source ID (default: 0) |
| `atlas_x` | number | no | Atlas X coordinate (default: 0) |
| `atlas_y` | number | no | Atlas Y coordinate (default: 0) |
| `alternative` | number | no | Alternative tile ID (default: 0) |
| `layer` | number | no | Layer index for deprecated multi-layer TileMap nodes (default: 0; TileMapLayer has one implicit layer and only accepts 0) |

### tilemap_get_cell

Get tile data at a specific cell in a TileMapLayer (or a deprecated multi-layer TileMap node)

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the TileMapLayer or deprecated TileMap node |
| `x` | number | yes | Cell X coordinate |
| `y` | number | yes | Cell Y coordinate |
| `layer` | number | no | Layer index for deprecated multi-layer TileMap nodes (default: 0; TileMapLayer has one implicit layer and only accepts 0) |

### tilemap_clear

Clear cells in a TileMapLayer (or a deprecated multi-layer TileMap node). For a legacy TileMap, omit layer to clear all layers or pass a layer to clear just that one

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the TileMapLayer or deprecated TileMap node |
| `layer` | number | no | Layer index for deprecated multi-layer TileMap nodes; omit to clear all layers (TileMapLayer only accepts 0) |

### tilemap_get_info

Get TileMapLayer (or deprecated multi-layer TileMap) info including tile set sources, per-layer breakdown, and cell count

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the TileMapLayer or deprecated TileMap node |

### tilemap_get_used_cells

Get a list of used (non-empty) cells in a TileMapLayer (or a deprecated multi-layer TileMap node)

| Parameter | Type | Required | Description |
|---|---|---|---|
| `node_path` | string | yes | Path to the TileMapLayer or deprecated TileMap node |
| `max_count` | number | no | Maximum cells to return (default: 500) |
| `layer` | number | no | Layer index for deprecated multi-layer TileMap nodes (default: 0; TileMapLayer has one implicit layer and only accepts 0) |

## Analysis (6)

### find_unused_resources

Scan the project for resource files (.tres, .tscn, .png, .wav, .ogg, .ttf, .gdshader, etc.) that are not referenced by any .tscn, .gd, or .tres file. Useful for cleaning up unused assets.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `path` | string | no | Root path to scan (default: res://) |
| `include_addons` | boolean | no | Include addons/ directory in scan (default: false) |

### analyze_signal_flow

Map all signal connections in the currently edited scene. Returns a graph-like structure showing which nodes emit which signals and which nodes receive them.

No parameters.

### analyze_scene_complexity

Analyze a scene's complexity: total node count, max nesting depth, nodes grouped by type, attached scripts, and potential issues (too many nodes, deep nesting).

| Parameter | Type | Required | Description |
|---|---|---|---|
| `path` | string | no | Scene path to analyze (default: currently edited scene) |

### find_script_references

Find all places where a given script path, class_name, or resource path is referenced across the project. Searches .tscn, .gd, and .tres files.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `query` | string | yes | The script path, class_name, or resource path to search for (e.g. 'res://scripts/player.gd', 'PlayerController', 'res://assets/icon.png') |
| `path` | string | no | Root path to search (default: res://) |
| `include_addons` | boolean | no | Include addons/ directory in search (default: false) |

### detect_circular_dependencies

Check for circular scene dependencies where Scene A instances Scene B which instances Scene A (directly or indirectly). Walks all .tscn files and builds a dependency graph.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `path` | string | no | Root path to scan (default: res://) |
| `include_addons` | boolean | no | Include addons/ directory in scan (default: false) |

### get_project_statistics

Get overall project statistics: file counts by extension, total script lines, scene count, resource count, autoload list, and enabled plugins.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `path` | string | no | Root path to scan (default: res://) |
| `include_addons` | boolean | no | Include addons/ directory in statistics (default: false) |

## Android (3)

### list_android_devices

List Android devices visible to adb (parses 'adb devices -l'). Uses the path configured in Editor Settings > Export > Android > Adb, falls back to 'adb' on PATH.

No parameters.

### get_android_preset_info

Read metadata (package name, export path, runnable flag) from an Android export preset in export_presets.cfg. If no preset is specified, returns the first Android preset.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `preset_name` | string | no | Preset name as shown in Project > Export |
| `preset_index` | number | no | Preset index (alternative to name) |

### deploy_to_android

Export APK via Godot CLI, install it on a connected Android device via adb, and optionally launch the main activity. Equivalent to Godot's Remote Deploy button. Requires a configured Android export preset and adb on PATH (or set in Editor Settings). This call is synchronous and may take tens of seconds to complete.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `preset_name` | string | no | Android export preset name (defaults to first Android preset) |
| `preset_index` | number | no | Preset index (alternative to name) |
| `device_serial` | string | no | adb device serial (omit to use default device) |
| `debug` | boolean | no | Debug export (default: true) |
| `launch` | boolean | no | Launch the app after install (default: true) |
| `skip_export` | boolean | no | Skip the export step and install the existing APK at the preset's export_path (default: false) |

## Headless (3)

### run_headless_scene

Run a scene in a separate headless Godot process and return its stdout/stderr and exit code. This is how to run a project's own test suite (e.g. res://tests/smoke_runner.tscn) — the editor-driven tools cannot see CLI-run tests. The scene should quit on its own; otherwise set quit_after_frames or timeout_sec.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `scene_path` | string | yes | Scene to run (e.g. 'res://tests/smoke_runner.tscn') |
| `args` | array | no | Extra arguments passed to the project itself (after the '--' separator). On Windows, arguments containing a double quote or a line break are rejected — a batch runner cannot represent the first and the second would end the command line. |
| `timeout_sec` | number | no | Kill the process after this many seconds (default 120, max 900). Partial output is still returned. |
| `quit_after_frames` | number | no | Pass --quit-after N so the run stops after N frames. Use for scenes that do not quit on their own. |

### run_headless_script

Run a script with `godot --headless --script` in a separate process and return its stdout/stderr and exit code. Use for `extends SceneTree` probe/tool scripts: they can instantiate scenes, call flow functions and read Control rects without a GPU.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `script_path` | string | yes | Script to run (e.g. 'res://tools/resolution_sweep.gd'). Should extend SceneTree or MainLoop. |
| `args` | array | no | Extra arguments passed to the project itself (after the '--' separator). On Windows, arguments containing a double quote or a line break are rejected — a batch runner cannot represent the first and the second would end the command line. |
| `timeout_sec` | number | no | Kill the process after this many seconds (default 120, max 900). Partial output is still returned. |
| `quit_after_frames` | number | no | Pass --quit-after N so the run stops after N frames. Use for scenes that do not quit on their own. |

### get_godot_executable

Get the path of the Godot binary running the editor, plus the absolute project path and platform. Use it to shell out to Godot consistently from bash instead of guessing the install location.

No parameters.
