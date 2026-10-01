#!/usr/bin/env node
// Sends one JSON-RPC command to the Godot MCP editor plugin and prints the result.
// Use it for plugin commands that the MCP CLI does not offer (for example set_input_action).
//
// Usage: node scripts/godot-rpc.mjs <method> '<params as JSON>'
// Example: node scripts/godot-rpc.mjs get_input_actions '{}'
//
// The Godot plugin is a WebSocket client: it connects to a server on ports 6505-6514 every few seconds.
// This script opens such a server, waits for the plugin, sends the command, and exits.
// It uses the "ws" package installed with the MCP server (mcp/godot-mcp-pro-*/server/node_modules).
import { createRequire } from "node:module";
import { randomUUID } from "node:crypto";
import { readdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const serverDir = readdirSync(join(root, "mcp"))
  .filter((name) => name.startsWith("godot-mcp-pro"))
  .map((name) => join(root, "mcp", name, "server"))[0];
const require = createRequire(join(serverDir, "package.json"));
const { WebSocketServer } = require("ws");

const [method, paramsJson = "{}"] = process.argv.slice(2);
if (!method) {
  console.error("Usage: node scripts/godot-rpc.mjs <method> '<params as JSON>'");
  process.exit(1);
}
const params = JSON.parse(paramsJson);

async function listen() {
  for (let port = 6510; port <= 6514; port++) {
    try {
      return await new Promise((resolve, reject) => {
        const wss = new WebSocketServer({ port, host: "127.0.0.1" });
        wss.once("listening", () => resolve(wss));
        wss.once("error", reject);
      });
    } catch {
      // Port in use: try the next one.
    }
  }
  throw new Error("No free port in 6510-6514.");
}

const wss = await listen();
const timeout = setTimeout(() => {
  console.error("The Godot plugin did not connect within 20 s. Is the editor open with the MCP plugin enabled?");
  process.exit(1);
}, 20000);

wss.on("connection", (ws) => {
  clearTimeout(timeout);
  const id = randomUUID();
  ws.on("message", (data) => {
    let message;
    try {
      message = JSON.parse(data.toString());
    } catch {
      return;
    }
    if (message.id !== id) {
      return;
    }
    if (message.error) {
      console.error(JSON.stringify(message.error, null, 2));
      process.exit(1);
    }
    console.log(JSON.stringify(message.result, null, 2));
    ws.close();
    wss.close();
    process.exit(0);
  });
  ws.send(JSON.stringify({ jsonrpc: "2.0", method, params, id }));
});
