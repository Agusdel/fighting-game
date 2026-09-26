import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
/**
 * Minimal mode: only these tools are registered (~35 tools).
 * Designed for clients with tight tool limits (Cursor: 40, local LLMs with small context).
 */
export declare const MINIMAL_TOOLS: Set<string>;
/**
 * 3D mode (`--3d`) is for clients with a hard 100-tool cap. It registers the
 * core tools plus Physics, AnimationTree and Navigation, minus these core
 * tools, which either have a direct alternative or matter little for 3D work.
 * Keep the resulting total at or under 100 (tests/modes.test.ts enforces it).
 */
export declare const THREED_EXCLUDED_TOOLS: Set<string>;
/** Hard cap that --3d mode must respect. */
export declare const THREED_TOOL_LIMIT = 100;
/**
 * Creates a proxy around McpServer that skips registrations named in denySet.
 */
export declare function createExcludingServer(server: McpServer, denySet: Set<string>): McpServer;
/**
 * Creates a proxy around McpServer that filters tool registrations.
 * Only tools in the allowSet will be registered; others are silently skipped.
 */
export declare function createFilteredServer(server: McpServer, allowSet: Set<string>): McpServer;
//# sourceMappingURL=tool-filter.d.ts.map