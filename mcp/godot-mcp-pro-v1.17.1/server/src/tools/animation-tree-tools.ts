import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { GodotConnection } from "../godot-connection.js";
import { formatErrorForMcp } from "../utils/errors.js";

export function registerAnimationTreeTools(
  server: McpServer,
  godot: GodotConnection
): void {
  server.tool(
    "create_animation_tree",
    "Create an AnimationTree node with an AnimationNodeStateMachine as root, optionally linked to an AnimationPlayer",
    {
      node_path: z.string().describe("Path to the parent node where the AnimationTree will be added"),
      anim_player: z.string().optional().describe("Relative path from the AnimationTree to the AnimationPlayer (e.g. '../AnimationPlayer')"),
      name: z.string().optional().describe("Name for the AnimationTree node (default: 'AnimationTree')"),
    },
    async (params) => {
      try {
        const result = await godot.sendCommand("create_animation_tree", params);
        return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
      } catch (e) {
        return { content: [{ type: "text", text: formatErrorForMcp(e) }], isError: true };
      }
    }
  );

  server.tool(
    "get_animation_tree_structure",
    "Read the full structure of an AnimationTree including all states, transitions, blend tree nodes, and blend space points (with point names on Godot 4.7+, sync_mode/cyclic_length on 4.7+, OneShot abort_on_reset on 4.6+)",
    {
      node_path: z.string().describe("Path to the AnimationTree node"),
    },
    async (params) => {
      try {
        const result = await godot.sendCommand("get_animation_tree_structure", params);
        return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
      } catch (e) {
        return { content: [{ type: "text", text: formatErrorForMcp(e) }], isError: true };
      }
    }
  );

  server.tool(
    "add_state_machine_state",
    "Add a state to an AnimationNodeStateMachine (animation clip, blend tree, nested state machine, or 1D/2D blend space with points)",
    {
      node_path: z.string().describe("Path to the AnimationTree node"),
      state_name: z.string().describe("Name for the new state"),
      state_type: z.enum(["animation", "blend_tree", "state_machine", "blend_space_1d", "blend_space_2d"]).optional().describe("Type of state: 'animation' (default), 'blend_tree', 'state_machine', 'blend_space_1d', or 'blend_space_2d'"),
      animation: z.string().optional().describe("Animation name to play (only for state_type='animation')"),
      blend_points: z
        .array(
          z.object({
            animation: z.string().optional().describe("Animation name played at this point"),
            position: z.union([z.number(), z.object({ x: z.number(), y: z.number() }), z.array(z.number()).length(2)]).describe("Point position: a number for 1D, {x,y} or [x,y] for 2D"),
            name: z.string().optional().describe("Point name (Godot 4.7+; defaults to the animation name on 4.7, error on older Godot)"),
          })
        )
        .max(64)
        .optional()
        .describe("Blend space points (blend space types only; Godot allows at most 64)"),
      min_space: z.union([z.number(), z.object({ x: z.number(), y: z.number() }), z.array(z.number()).length(2)]).optional().describe("Blend space minimum (number for 1D, {x,y} for 2D)"),
      max_space: z.union([z.number(), z.object({ x: z.number(), y: z.number() }), z.array(z.number()).length(2)]).optional().describe("Blend space maximum (number for 1D, {x,y} for 2D)"),
      sync: z.boolean().optional().describe("Blend space: keep non-dominant animations advancing (all versions)"),
      sync_mode: z.enum(["none", "independent", "cyclic_mutable", "cyclic_constant"]).optional().describe("Blend space sync mode. 'cyclic_mutable'/'cyclic_constant' need Godot 4.7+; 'none'/'independent' map to the sync bool on older Godot"),
      cyclic_length: z.number().optional().describe("Blend space cyclic sync length in seconds (Godot 4.7+)"),
      state_machine_path: z.string().optional().describe("Slash-separated path to a nested state machine (e.g. 'Run/SubState'). Empty or omit for root."),
      position_x: z.number().optional().describe("X position in the graph editor (default: 0)"),
      position_y: z.number().optional().describe("Y position in the graph editor (default: 0)"),
    },
    async (params) => {
      try {
        const result = await godot.sendCommand("add_state_machine_state", params);
        return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
      } catch (e) {
        return { content: [{ type: "text", text: formatErrorForMcp(e) }], isError: true };
      }
    }
  );

  server.tool(
    "remove_state_machine_state",
    "Remove a state from an AnimationNodeStateMachine (also removes connected transitions)",
    {
      node_path: z.string().describe("Path to the AnimationTree node"),
      state_name: z.string().describe("Name of the state to remove"),
      state_machine_path: z.string().optional().describe("Slash-separated path to a nested state machine. Empty or omit for root."),
    },
    async (params) => {
      try {
        const result = await godot.sendCommand("remove_state_machine_state", params);
        return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
      } catch (e) {
        return { content: [{ type: "text", text: formatErrorForMcp(e) }], isError: true };
      }
    }
  );

  server.tool(
    "add_state_machine_transition",
    "Add a transition between two states in an AnimationNodeStateMachine with configurable switch mode, advance mode, and expression conditions",
    {
      node_path: z.string().describe("Path to the AnimationTree node"),
      from_state: z.string().describe("Source state name (use 'Start' for the entry point)"),
      to_state: z.string().describe("Destination state name (use 'End' for the exit point)"),
      switch_mode: z.enum(["at_end", "immediate", "sync"]).optional().describe("When to switch: 'at_end' (wait for animation), 'immediate' (default), 'sync'"),
      advance_mode: z.enum(["disabled", "enabled", "auto"]).optional().describe("How to advance: 'disabled', 'enabled' (default, uses travel), 'auto' (automatic)"),
      advance_expression: z.string().optional().describe("GDScript expression that triggers this transition (e.g. 'is_running')"),
      xfade_time: z.number().optional().describe("Cross-fade time in seconds"),
      state_machine_path: z.string().optional().describe("Slash-separated path to a nested state machine. Empty or omit for root."),
    },
    async (params) => {
      try {
        const result = await godot.sendCommand("add_state_machine_transition", params);
        return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
      } catch (e) {
        return { content: [{ type: "text", text: formatErrorForMcp(e) }], isError: true };
      }
    }
  );

  server.tool(
    "remove_state_machine_transition",
    "Remove a transition between two states in an AnimationNodeStateMachine",
    {
      node_path: z.string().describe("Path to the AnimationTree node"),
      from_state: z.string().describe("Source state name"),
      to_state: z.string().describe("Destination state name"),
      state_machine_path: z.string().optional().describe("Slash-separated path to a nested state machine. Empty or omit for root."),
    },
    async (params) => {
      try {
        const result = await godot.sendCommand("remove_state_machine_transition", params);
        return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
      } catch (e) {
        return { content: [{ type: "text", text: formatErrorForMcp(e) }], isError: true };
      }
    }
  );

  server.tool(
    "set_blend_tree_node",
    "Add or replace a node inside an AnimationNodeBlendTree state (Add2, Blend2, TimeScale, Animation, OneShot, BlendSpace1D/2D, etc.) with optional connection",
    {
      node_path: z.string().describe("Path to the AnimationTree node"),
      blend_tree_state: z.string().describe("Name of the BlendTree state in the state machine"),
      bt_node_name: z.string().describe("Name for the node inside the BlendTree"),
      bt_node_type: z.enum(["Animation", "Add2", "Blend2", "Add3", "Blend3", "TimeScale", "TimeSeek", "Transition", "OneShot", "Sub2", "BlendSpace1D", "BlendSpace2D"]).describe("Type of BlendTree node to create"),
      animation: z.string().optional().describe("Animation name (only for bt_node_type='Animation')"),
      abort_on_reset: z.boolean().optional().describe("OneShot only (Godot 4.6+): abort the one-shot when the tree is reset"),
      blend_points: z
        .array(
          z.object({
            animation: z.string().optional().describe("Animation name played at this point"),
            position: z.union([z.number(), z.object({ x: z.number(), y: z.number() }), z.array(z.number()).length(2)]).describe("Point position: a number for 1D, {x,y} or [x,y] for 2D"),
            name: z.string().optional().describe("Point name (Godot 4.7+; defaults to the animation name on 4.7, error on older Godot)"),
          })
        )
        .max(64)
        .optional()
        .describe("Blend space points (blend space types only; Godot allows at most 64)"),
      min_space: z.union([z.number(), z.object({ x: z.number(), y: z.number() }), z.array(z.number()).length(2)]).optional().describe("Blend space minimum (number for 1D, {x,y} for 2D)"),
      max_space: z.union([z.number(), z.object({ x: z.number(), y: z.number() }), z.array(z.number()).length(2)]).optional().describe("Blend space maximum (number for 1D, {x,y} for 2D)"),
      sync: z.boolean().optional().describe("Blend space: keep non-dominant animations advancing (all versions)"),
      sync_mode: z.enum(["none", "independent", "cyclic_mutable", "cyclic_constant"]).optional().describe("Blend space sync mode. 'cyclic_mutable'/'cyclic_constant' need Godot 4.7+; 'none'/'independent' map to the sync bool on older Godot"),
      cyclic_length: z.number().optional().describe("Blend space cyclic sync length in seconds (Godot 4.7+)"),
      connect_to: z.string().optional().describe("Name of another BlendTree node to connect this node's output to"),
      connect_port: z.number().optional().describe("Input port index on the target node (default: 0)"),
      state_machine_path: z.string().optional().describe("Slash-separated path to a nested state machine. Empty or omit for root."),
      position_x: z.number().optional().describe("X position in the graph editor (default: 0)"),
      position_y: z.number().optional().describe("Y position in the graph editor (default: 0)"),
    },
    async (params) => {
      try {
        const result = await godot.sendCommand("set_blend_tree_node", params);
        return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
      } catch (e) {
        return { content: [{ type: "text", text: formatErrorForMcp(e) }], isError: true };
      }
    }
  );

  server.tool(
    "set_tree_parameter",
    "Set an AnimationTree parameter value (conditions, blend amounts, time scale, etc.)",
    {
      node_path: z.string().describe("Path to the AnimationTree node"),
      parameter: z.string().describe("Parameter path (e.g. 'conditions/is_running', 'Blend2/blend_amount'). 'parameters/' prefix is auto-added if missing."),
      value: z.union([z.string(), z.number(), z.boolean()]).describe("Parameter value. Strings are auto-parsed for Vector2, Color, etc."),
    },
    async (params) => {
      try {
        const result = await godot.sendCommand("set_tree_parameter", params);
        return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
      } catch (e) {
        return { content: [{ type: "text", text: formatErrorForMcp(e) }], isError: true };
      }
    }
  );

  const ikSetting = {
    root_bone: z.string().optional().describe("Root bone name of the chain (required)"),
    middle_bone: z.string().optional().describe("TwoBoneIK3D only: middle bone (e.g. the elbow/knee), must descend from root_bone"),
    end_bone: z.string().optional().describe("End bone name, must descend from root_bone (TwoBoneIK3D: from middle_bone). Optional for TwoBoneIK3D when use_virtual_end=true"),
    target_path: z.string().optional().describe("Scene path of the Node3D the chain reaches for (TwoBoneIK3D, CCDIK3D, FABRIK3D, JacobianIK3D). Stored relative to the modifier"),
    pole_path: z.string().optional().describe("TwoBoneIK3D only: scene path of the pole Node3D"),
    pole_direction: z.enum(["none", "+x", "-x", "+y", "-y", "+z", "-z", "custom"]).optional().describe("TwoBoneIK3D only: pole direction axis"),
    pole_direction_vector: z.object({ x: z.number(), y: z.number(), z: z.number() }).optional().describe("TwoBoneIK3D only: custom pole direction (sets pole_direction='custom')"),
    path_3d: z.string().optional().describe("SplineIK3D only (required): scene path of the Path3D the chain follows"),
    tilt_enabled: z.boolean().optional().describe("SplineIK3D only: apply the curve tilt"),
    use_virtual_end: z.boolean().optional().describe("TwoBoneIK3D only: use a virtual end bone extended from middle_bone"),
    extend_end_bone: z.boolean().optional().describe("Extend the end bone by end_bone_length (auto-enabled when end_bone_length/direction is given)"),
    end_bone_length: z.number().optional().describe("Virtual extension length in meters"),
    end_bone_direction: z.enum(["+x", "-x", "+y", "-y", "+z", "-z", "from_parent"]).optional().describe("Direction of the end bone extension"),
  };

  server.tool(
    "setup_ik_modifier",
    "Add an IK SkeletonModifier3D (Godot 4.6+: TwoBoneIK3D, CCDIK3D, FABRIK3D, JacobianIK3D, SplineIK3D) as a child of a Skeleton3D and configure its chain(s): root/middle/end bones (validated against the skeleton and its hierarchy), target, pole or Path3D. One chain can be given with top-level fields, several with 'settings'. Undoable. On Godot 4.5 returns an error naming the running version.",
    {
      skeleton_path: z.string().describe("Path to the Skeleton3D node"),
      ik_type: z.enum(["TwoBoneIK3D", "CCDIK3D", "FABRIK3D", "JacobianIK3D", "SplineIK3D"]).optional().describe("IK modifier class (default: TwoBoneIK3D)"),
      name: z.string().optional().describe("Node name (default: the ik_type)"),
      ...ikSetting,
      settings: z
        .array(z.object(ikSetting))
        .optional()
        .describe("One entry per IK chain (setting_count = length). Overrides the top-level chain fields"),
      influence: z.number().optional().describe("Modifier influence 0-1 (default: 1)"),
      active: z.boolean().optional().describe("Whether the modifier is active (default: true)"),
      mutable_bone_axes: z.boolean().optional().describe("Allow bone axes to change during solving"),
      max_iterations: z.number().int().optional().describe("CCDIK3D/FABRIK3D/JacobianIK3D: max solver iterations"),
      min_distance: z.number().optional().describe("CCDIK3D/FABRIK3D/JacobianIK3D: stop when the end is this close to the target"),
      angular_delta_limit: z.number().optional().describe("CCDIK3D/FABRIK3D/JacobianIK3D: max rotation per iteration, in degrees"),
      deterministic: z.boolean().optional().describe("CCDIK3D/FABRIK3D/JacobianIK3D: solve from the rest pose each frame"),
    },
    async (params) => {
      try {
        const result = await godot.sendCommand("setup_ik_modifier", params);
        return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
      } catch (e) {
        return { content: [{ type: "text", text: formatErrorForMcp(e) }], isError: true };
      }
    }
  );
}
