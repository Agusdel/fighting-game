import { z } from "zod";
import { formatErrorForMcp } from "../utils/errors.js";
export function registerExportTools(server, godot) {
    server.tool("list_export_presets", "List all export presets configured in export_presets.cfg", {}, async () => {
        try {
            const result = await godot.sendCommand("list_export_presets");
            return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
        }
        catch (e) {
            return { content: [{ type: "text", text: formatErrorForMcp(e) }], isError: true };
        }
    });
    server.tool("export_project", "Get the export command for a preset (direct export from editor is not supported in Godot 4)", {
        preset_name: z.string().optional().describe("Export preset name"),
        preset_index: z.number().optional().describe("Export preset index (alternative to name)"),
        debug: z.boolean().optional().describe("Debug export (default: true)"),
    }, async (params) => {
        try {
            const result = await godot.sendCommand("export_project", params);
            return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
        }
        catch (e) {
            return { content: [{ type: "text", text: formatErrorForMcp(e) }], isError: true };
        }
    });
    server.tool("export_patch_pck", "Export a patch PCK that contains only the files changed since the given base packs (Godot's export_pack_patch, 4.4+). Runs Godot's own `--export-patch` CLI in a headless child process with the project's configured preset, waits for it, and reports the output size. Exports what is saved on disk (save scenes first) and always in release mode (the CLI has no debug variant). Blocks until done (default timeout 300s).", {
        preset_name: z.string().optional().describe("Export preset name (see list_export_presets)"),
        preset_index: z.number().optional().describe("Export preset index (alternative to name)"),
        output_path: z.string().describe("Where to write the patch, ending in .pck: res://, user:// or an absolute path. Its directory must already exist. Overwritten if present."),
        patches: z.array(z.string()).optional().describe("Base .pck files the shipped game already has (res://, user:// or absolute paths; each must exist). Files identical to these are left out of the patch. If omitted, the preset's own Patches list is used."),
        timeout_sec: z.number().optional().describe("Give up after this many seconds (10-1800, default 300)"),
    }, async (params) => {
        try {
            // The addon waits for the child export itself; give the RPC that long plus slack.
            const timeoutSec = Math.min(Math.max(params.timeout_sec ?? 300, 10), 1800);
            const result = await godot.sendCommand("export_patch_pck", params, timeoutSec * 1000 + 15_000);
            return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
        }
        catch (e) {
            return { content: [{ type: "text", text: formatErrorForMcp(e) }], isError: true };
        }
    });
    server.tool("get_export_info", "Get export-related project info (executable path, templates, project path)", {}, async () => {
        try {
            const result = await godot.sendCommand("get_export_info");
            return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
        }
        catch (e) {
            return { content: [{ type: "text", text: formatErrorForMcp(e) }], isError: true };
        }
    });
}
//# sourceMappingURL=export-tools.js.map