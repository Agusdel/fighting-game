import { z } from "zod";
import { formatErrorForMcp } from "../utils/errors.js";
/** A Vector2 given as 'Vector2(x, y)', {x, y} or [x, y]. */
const vector2Param = z.union([
    z.string(),
    z.object({ x: z.number(), y: z.number() }),
    z.array(z.number()).length(2),
]);
export function registerThemeTools(server, godot) {
    server.tool("create_theme", "Create a new Theme resource file", {
        path: z.string().describe("Path to save the theme (e.g. 'res://themes/main.tres')"),
        default_font_size: z.number().optional().describe("Default font size"),
    }, async (params) => {
        try {
            const result = await godot.sendCommand("create_theme", params);
            return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
        }
        catch (e) {
            return { content: [{ type: "text", text: formatErrorForMcp(e) }], isError: true };
        }
    });
    server.tool("set_theme_color", "Set a theme color override on a Control node", {
        node_path: z.string().describe("Path to the Control node"),
        name: z.string().describe("Color name (e.g. 'font_color', 'font_hover_color')"),
        color: z.string().describe("Color as hex string (e.g. '#ff0000') or name"),
        theme_type: z.string().optional().describe("Theme type (defaults to node's class)"),
    }, async (params) => {
        try {
            const result = await godot.sendCommand("set_theme_color", params);
            return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
        }
        catch (e) {
            return { content: [{ type: "text", text: formatErrorForMcp(e) }], isError: true };
        }
    });
    server.tool("set_theme_constant", "Set a theme constant override on a Control node", {
        node_path: z.string().describe("Path to the Control node"),
        name: z.string().describe("Constant name (e.g. 'margin_left', 'separation')"),
        value: z.number().describe("Integer value"),
    }, async (params) => {
        try {
            const result = await godot.sendCommand("set_theme_constant", params);
            return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
        }
        catch (e) {
            return { content: [{ type: "text", text: formatErrorForMcp(e) }], isError: true };
        }
    });
    server.tool("set_theme_font_size", "Set a theme font size override on a Control node", {
        node_path: z.string().describe("Path to the Control node"),
        name: z.string().describe("Font size name (e.g. 'font_size')"),
        size: z.number().describe("Font size in pixels"),
    }, async (params) => {
        try {
            const result = await godot.sendCommand("set_theme_font_size", params);
            return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
        }
        catch (e) {
            return { content: [{ type: "text", text: formatErrorForMcp(e) }], isError: true };
        }
    });
    server.tool("set_theme_stylebox", "Set a StyleBoxFlat override on a Control node with customizable appearance", {
        node_path: z.string().describe("Path to the Control node"),
        name: z.string().describe("Style name (e.g. 'panel', 'normal', 'hover', 'pressed')"),
        bg_color: z.string().optional().describe("Background color (hex)"),
        border_color: z.string().optional().describe("Border color (hex)"),
        border_width: z.number().optional().describe("Border width in pixels"),
        corner_radius: z.number().optional().describe("Corner radius in pixels"),
        padding: z.number().optional().describe("Content padding in pixels"),
    }, async (params) => {
        try {
            const result = await godot.sendCommand("set_theme_stylebox", params);
            return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
        }
        catch (e) {
            return { content: [{ type: "text", text: formatErrorForMcp(e) }], isError: true };
        }
    });
    server.tool("setup_control", "Configure a Control/Container node's layout properties in one call. Sets anchor preset, margins, min size, size flags, and container-specific properties like separation — much faster than multiple update_property calls. Newer Control properties are only set when passed: pivot_offset_ratio (Godot 4.6+), max_size and offset_transform (Godot 4.7+); on older versions passing them returns an error. Undoable as one action.", {
        node_path: z.string().describe("Path to the Control node"),
        anchor_preset: z.string().optional().describe("Anchor preset: 'top_left', 'top_right', 'bottom_left', 'bottom_right', 'center_left', 'center_top', 'center_right', 'center_bottom', 'center', 'left_wide', 'top_wide', 'right_wide', 'bottom_wide', 'vcenter_wide', 'hcenter_wide', 'full_rect'"),
        min_size: z.string().optional().describe("Minimum size as 'Vector2(w, h)'"),
        size_flags_h: z.string().optional().describe("Horizontal size flags: 'fill', 'expand', 'fill_expand', 'shrink_center', 'shrink_end'"),
        size_flags_v: z.string().optional().describe("Vertical size flags: 'fill', 'expand', 'fill_expand', 'shrink_center', 'shrink_end'"),
        margins: z.object({
            left: z.number().optional(),
            top: z.number().optional(),
            right: z.number().optional(),
            bottom: z.number().optional(),
        }).optional().describe("Margin overrides for MarginContainer (sets theme constants margin_left/right/top/bottom)"),
        separation: z.number().optional().describe("Separation for VBoxContainer/HBoxContainer (theme constant override)"),
        grow_h: z.string().optional().describe("Horizontal grow direction: 'begin', 'end', 'both'"),
        grow_v: z.string().optional().describe("Vertical grow direction: 'begin', 'end', 'both'"),
        pivot_offset_ratio: vector2Param.optional().describe("Godot 4.6+. Pivot for rotation/scale as a fraction of the size, e.g. 'Vector2(0.5, 0.5)' = centre. Added to pivot_offset (pixels); unlike it, follows resizes."),
        max_size: vector2Param.optional().describe("Godot 4.7+. Sets custom_maximum_size, e.g. 'Vector2(400, 300)'. A negative axis (the default -1) means no maximum."),
        offset_transform: z.object({
            position: vector2Param.optional().describe("Offset in pixels"),
            position_ratio: vector2Param.optional().describe("Offset as a fraction of the size"),
            scale: vector2Param.optional(),
            rotation_degrees: z.number().optional(),
            pivot: vector2Param.optional().describe("Pivot in pixels"),
            pivot_ratio: vector2Param.optional().describe("Pivot as a fraction of the size"),
            visual_only: z.boolean().optional().describe("true (Godot's default): only drawing moves, input still hits the untransformed rect; false: input follows the transform"),
            enabled: z.boolean().optional().describe("Defaults to true when any other key is given"),
        }).optional().describe("Godot 4.7+. Control offset transform (offset_transform_* properties): moves/rotates/scales a Control after layout without fighting its container — handy for hover/press animations inside containers."),
    }, async (params) => {
        try {
            const result = await godot.sendCommand("setup_control", params);
            return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
        }
        catch (e) {
            return { content: [{ type: "text", text: formatErrorForMcp(e) }], isError: true };
        }
    });
    server.tool("add_virtual_joystick", "Add a VirtualJoystick (Godot 4.7+ on-screen touch joystick) that presses the given input actions. Returns an error on older versions. Validates enum values and ranges; warns (does not fail) when an action is missing from the project's Input Map. The node's rect is its touch area, so a size is always set (default: 2.5x joystick_size, square). It reacts to touch only: enable input_devices/pointing/emulate_touch_from_mouse to test it with a mouse. Appearance comes from the theme styleboxes normal_joystick, normal_tip, pressed_joystick and pressed_tip (set_theme_stylebox). Undoable.", {
        parent_path: z.string().optional().describe("Parent node path (default: scene root '.'). Usually a CanvasLayer or full-rect Control for HUDs."),
        name: z.string().optional().describe("Node name (default: 'VirtualJoystick')"),
        action_left: z.string().optional().describe("Input action pressed when pushed left (default: 'ui_left')"),
        action_right: z.string().optional().describe("Input action pressed when pushed right (default: 'ui_right')"),
        action_up: z.string().optional().describe("Input action pressed when pushed up (default: 'ui_up')"),
        action_down: z.string().optional().describe("Input action pressed when pushed down (default: 'ui_down')"),
        mode: z.enum(["fixed", "dynamic", "following"]).optional().describe("fixed: stays where placed; dynamic: appears where the touch starts; following: moves with a finger dragged past its edge (default: fixed)"),
        visibility: z.enum(["always", "when_touched"]).optional().describe("Default: always"),
        joystick_size: z.number().optional().describe("Base circle diameter in px, 10-500 (default 100)"),
        tip_size: z.number().optional().describe("Tip diameter in px, 5-250 (default 50)"),
        deadzone_ratio: z.number().optional().describe("0-1 fraction of the radius ignored (default 0)"),
        clampzone_ratio: z.number().optional().describe("0-2, how far the tip may travel relative to the radius (default 1)"),
        initial_offset_ratio: vector2Param.optional().describe("Where the stick rests inside the node's rect, as a fraction (default 'Vector2(0.5, 0.5)')"),
        size: vector2Param.optional().describe("Touch-area size in px, e.g. 'Vector2(300, 300)'"),
        anchor_preset: z.string().optional().describe("Anchor preset keeping the size, e.g. 'bottom_left' or 'bottom_right' (same names as setup_control). Exclusive with position."),
        margin: z.number().optional().describe("Distance from the anchored edges in px when anchor_preset is set (default 0)"),
        position: vector2Param.optional().describe("Position in px when no anchor_preset is given"),
    }, async (params) => {
        try {
            const result = await godot.sendCommand("add_virtual_joystick", params);
            return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
        }
        catch (e) {
            return { content: [{ type: "text", text: formatErrorForMcp(e) }], isError: true };
        }
    });
    server.tool("get_theme_info", "Get theme information and overrides for a Control node", {
        node_path: z.string().describe("Path to the Control node"),
    }, async (params) => {
        try {
            const result = await godot.sendCommand("get_theme_info", params);
            return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
        }
        catch (e) {
            return { content: [{ type: "text", text: formatErrorForMcp(e) }], isError: true };
        }
    });
}
//# sourceMappingURL=theme-tools.js.map