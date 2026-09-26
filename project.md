# Fighting Game — Project Design Document

Status: **Draft**. This document is the source of truth for the design. Update it when a decision changes.

- Engine: Godot 4.8 (C# / .NET 8)
- Genre: 2D side-view platform fighter (Super Smash Bros. style), free-for-all, 2–4 players
- Scope: Simple prototype with a robust, deterministic foundation

---

## 1. Requirements

### 1.1 Game

- 2D, side view.
- Local and online multiplayer.
- Main menu: **Play Local**, **Play Online** (**Host** / **Join**).
- Lobby: shows connected players. The host can start the game. All players can return to the main menu.
- One stage: floor, ceiling, left wall, right wall, and one platform.
- Fixed camera. The full stage is visible at all times.

### 1.2 Multiplayer

- Local play: more than one player on one machine.
- Online play: more than one machine. Each machine can have more than one local player.
- Lockstep with rollback (GGPO-style).
- Fully deterministic gameplay.
- Fixed-point math in the simulation.
- Network manager with an abstract transport interface. Implementations: localhost/LAN, Steam SDK. New transports must be easy to add.

### 1.3 Player (fighter)

- Move left and right.
- Jump. Fall with gravity.
- Attack. Attacks use frame data and hitboxes (deterministic).
- Collision with floor, walls, ceiling, and platforms.
- Placeholder art (the user supplies it later).
- Placeholder animation. Animation must work with rollback.
- Health bar.

### 1.4 Scoring

- No scoring system at first.
- A fighter at 0 health is dead and stays out of the match.
- When only one fighter is alive, the match restarts. This repeats until the players quit.

### 1.5 Fairness

- The update order of players must not change the result.
- Each tick processes movement for all players first, then collisions for all players.
- Hits are resolved simultaneously (trades are possible).

### 1.6 Determinism

- The world state is laid out so it is easy to save, restore, and hash.
- Fixed-point math. No floating point in the simulation.
- Custom transforms. No Godot float positions in the simulation.
- Animations can jump to any frame (required for rollback).
- Determinism tool:
  - Record the initial world state and all inputs.
  - A slider to rewind and replay.
  - Hash comparison to prove two states are identical.
- Level and collision boxes are authored in the Godot editor. A deterministic conversion step turns them into fixed-point data.

---

## 2. Core Principles

1. **Simulation and presentation are separate.** The simulation is pure C#. It has no reference to Godot. Godot only reads the simulation state and draws it.
2. **The simulation is a pure function:** `NextState = Tick(State, Inputs)`. Same state + same inputs = same next state, on every machine.
3. **State is plain data.** The world state contains only value types (structs, ints, fixed-point numbers, fixed-size arrays). It contains no references, no class instances, no collections with undefined order.
4. **Static data is separate from state.** Stage geometry, move frame data, and character stats never change during a match. They are not saved or rolled back.
5. **Presentation is stateless relative to the simulation.** A view can render any frame from the state alone. This makes rollback and replay scrubbing free.

---

## 3. Architecture

### 3.1 Layers

```
+--------------------------------------------------------------+
| Presentation (Godot)                                         |
|  Scenes, UI, FighterView, StageView, HealthBar, ReplayViewer |
+--------------------------------------------------------------+
| Session / Netcode (pure C#)                                  |
|  RollbackSession, InputQueue, SyncTest, ReplayRecorder       |
+----------------------------+---------------------------------+
| Simulation (pure C#)       | Networking (transport)          |
|  WorldState, Tick, Physics |  INetworkTransport              |
|  Combat, FrameData         |  Loopback / ENet / Steam        |
+----------------------------+---------------------------------+
| Core (pure C#): Fixed, FixedVector2, FixedAabb, Rng, Hash    |
+--------------------------------------------------------------+
```

Only the Presentation layer and the Godot-specific transports use Godot APIs.

Core, Simulation, Session, and the Godot-free part of Networking are in a separate class library, `FightingGame.Simulation`. This library does not reference Godot, so the compiler blocks Godot types in the simulation. See section 4.

### 3.2 Fixed-point math

- Own implementation (no third-party fixed-point library).
- Type `Fixed`: a struct that wraps a `long` in **Q48.16** format (48 integer bits, 16 fraction bits).
- 1 world unit = 1 pixel.
- Operations: `+ - * /`, comparison, `Abs`, `Min`, `Max`, `Clamp`, `Sign`, `Floor`, `Ceil`, `Round`, integer `Sqrt` (if needed).
- No trigonometry is required for the first version.
- Explicit rounding rules (documented in code). Multiply: `(a * b) >> 16` with an arithmetic shift.
- Conversion from `float` is **not** allowed in the simulation. Conversion from `int` and from rational constants (`Fixed.FromRatio(3, 2)`) is allowed.
- Conversion to `float` is allowed only in the presentation layer.
- Types built on it: `FixedVector2`, `FixedAabb`.

### 3.3 World state

- `WorldState` is a struct (or a class that holds only fixed-size struct arrays) with:
  - `Frame` (int)
  - `Rng` state (deterministic PRNG, for example xorshift)
  - `MatchPhase` and phase timer (for example: countdown, fighting, restarting)
  - `FighterState[MaxPlayers]`
- `FighterState` (unmanaged struct):
  - Position, Velocity (`FixedVector2`)
  - Facing (int: -1 or +1). Set by the last horizontal move input (not by the opponent position)
  - Health (int)
  - Action (enum: Idle, Walk, JumpSquat, Airborne, Attack, Hitstun, Dead, …)
  - ActionFrame (int: frames since the action started)
  - Grounded flag, platform drop-through timer, hitstun timer
  - Hit registry for the current attack (bitmask of players already hit)
- Save: copy the struct memory to a `byte[]` (with `MemoryMarshal`).
- Restore: copy the `byte[]` back.
- Hash: FNV-1a 64-bit (or xxHash64) over the saved bytes.
- Rules: no `Dictionary`/`HashSet` iteration, no `DateTime`, no `System.Random`, no static mutable state, no floats.

### 3.4 Tick order (fairness)

Each `Tick(state, inputs)` runs phases. Each phase runs for **all** players before the next phase starts. A phase reads data from the previous phase only. So the player index order cannot change the result.

1. **Input and intent**: read input, update action state machine, set velocity (walk, jump, gravity).
2. **Movement and terrain collision**: move each player, resolve against static terrain only (axis-separated AABB: X first, then Y). Players do not affect each other in this phase.
3. **Hit detection**: find all hitbox/hurtbox overlaps from the positions after phase 2. Store them in a hit list.
4. **Hit resolution**: apply all hits at the same time (damage, hitstun, knockback). Trades are possible.
5. **Match rules**: mark dead fighters. When one fighter (or zero) is alive, start the restart timer. Reset the match when the timer ends.
6. `Frame++`.

Fighters do not collide with each other (no pushboxes). They pass through each other, like in Smash.

### 3.5 Collision

- Only axis-aligned boxes (AABB) in the first version.
- Terrain types:
  - **Solid**: floor, ceiling, walls. Blocks from all sides.
  - **One-way platform**: blocks only from above, only when the fighter falls. Down input drops through it.
- Boxes for fighters: collision box (terrain), hurtboxes, hitboxes. All boxes are relative to the fighter position and mirrored by `Facing`.

### 3.6 Frame data and animation

- Each action (idle, walk, jump, attack, …) has a `MoveData` definition (static data):
  - Total frame count, loop flag
  - Per-frame (or per-frame-range) data: sprite index, hitboxes, hurtboxes, damage, hitstun, knockback
- Knockback is a fixed velocity for each hitbox. It does not scale with damage or health. The X direction is mirrored by the attacker `Facing`.
- The stage is closed (walls and ceiling), so fighters cannot leave it. There are no blast zones.
- The simulation uses only `Action` + `ActionFrame`. The view picks the sprite from the same two values.
- The view sets the sprite frame directly each render (for example `Sprite2D.Frame` or `AnimatedSprite2D.Frame`). Godot never plays the animation on its own timer. So a rollback or a replay jump shows the correct frame immediately.

### 3.7 Editor authoring and conversion

- Stage and hitboxes are authored in the Godot editor with custom `[Tool]` nodes (for example `TerrainBoxAuthoring`, `PlatformAuthoring`, `SpawnPointAuthoring`). The nodes draw their boxes in the editor.
- Authored values must be **integer pixels**. The converter rounds with a fixed rule and reports an error if a value is not an integer. This removes all float ambiguity.
- The converter produces static data (`StageData`, `MoveData`) with `Fixed` values. The simulation uses only this data.
- The conversion runs at runtime when the stage loads (no bake step). Every machine converts the same integer values, so the result is the same.
- Conversion rule: `int value = Mathf.RoundToInt(authored)`. If `Math.Abs(authored - value) > 0.001`, the converter reports an error with the node path.

### 3.8 Input

- Each player's input for one frame is a bitmask (`ushort`): Left, Right, Up, Down, Jump, Attack, …
- `InputFrame` = inputs for all players for one frame.
- Local devices (keyboard sets, gamepads) map to player slots in the lobby.
- Godot input is polled once per simulation tick and converted to the bitmask. The simulation never reads Godot input.

### 3.9 Rollback session

- Fixed tick rate: **60 Hz**. The game loop runs the simulation with its own accumulator, not with `_PhysicsProcess`.
- Peer-to-peer topology. All peers run the full simulation.
- Each peer sends its local inputs for each frame to all other peers (redundant: each packet repeats unacknowledged inputs).
- Missing remote inputs are predicted (repeat the last known input).
- When a real input arrives and differs from the prediction: restore the state of that frame, and re-simulate to the current frame.
- State snapshots are kept in a ring buffer (size = max rollback window, for example 8–10 frames).
- Configurable input delay (for example 2 frames) to reduce rollbacks.
- Time sync: a peer that runs ahead of the others waits (skips a frame) so all peers stay close.
- Desync detection: peers exchange the hash of confirmed frames at an interval. A mismatch is reported.
- `SessionBase` with implementations: `LocalSession` (no network), `RollbackSession` (online), `SyncTestSession` (debug), `ReplaySession`.

### 3.10 Networking abstraction

```csharp
public interface INetworkTransport
{
    void Host(HostOptions options);
    void Join(JoinOptions options);
    void Send(PeerId peer, ReadOnlySpan<byte> data, DeliveryMode mode); // Reliable / Unreliable
    void Poll(); // Raises events: PeerConnected, PeerDisconnected, MessageReceived
    void Disconnect();
}
```

- `LoopbackTransport`: in memory. Simulated latency, jitter, and packet loss. For tests and for two sessions in one process.
- `EnetTransport`: Godot `ENetConnection` over UDP. For localhost and LAN.
- `SteamTransport`: Steam networking (Steamworks.NET or Facepunch.Steamworks). Later.
- The lobby layer (player slots, ready state, start message, match seed) is above the transport and is the same for all transports. Messages are reliable.
- The rollback session uses unreliable messages for inputs.
- `INetworkTransport`, the message protocol, and `LoopbackTransport` are in `FightingGame.Simulation`. `EnetTransport` and `SteamTransport` are in the Godot project.
- Disconnect: when a peer disconnects during a match, the match stops and all players return to the lobby.

### 3.11 Determinism tools

- **Replay recording**: initial state (or seed + config) + the input for every frame + the hash of every frame.
- **Replay viewer**: a scene with a timeline slider. Jump to any frame: restore the nearest keyframe snapshot (for example every 60 frames), then simulate forward. Play, pause, step one frame.
- **Hash check on replay**: re-simulate the replay and compare each frame hash with the recorded hash. Report the first frame that differs.
- **SyncTest mode** (from GGPO): on each frame, roll back N frames and re-simulate. Compare hashes. This finds bugs in save/restore and hidden state.
- **Headless runner**: run a replay file from the command line (no rendering). Compare hashes across machines and builds.
- **State diff**: when hashes differ, show the fields that differ (per fighter).

---

## 4. Project Layout

```
/                                   Repository root (docs, tests, tools)
  project.md                        This document
  tests/
    FightingGame.Simulation.Tests/  xUnit tests for the simulation library
  tools/
    ReplayRunner/                   Headless replay and hash checker (console app)
  project/                          Godot project (res://)
    project.godot
    FightingGame.csproj             Godot game assembly. References FightingGame.Simulation
    FightingGame.sln                Contains all projects
    simulation/                     Pure C# class library (no Godot reference)
      .gdignore                     Godot editor ignores this folder
      FightingGame.Simulation.csproj
      Core/                         Fixed, FixedVector2, FixedAabb, Rng, Hash
      Simulation/                   WorldState, Tick, physics, combat, static data types
      Session/                      Sessions, input queue, replay
      Networking/                   INetworkTransport, protocol, LoopbackTransport
    src/                            Godot C# code
      Presentation/                 Views, UI, HUD
      Authoring/                    [Tool] authoring nodes, converters to static data
      Networking/                   EnetTransport, SteamTransport
    scenes/                         MainMenu, Lobby, Match, ReplayViewer, stages, fighters
    assets/                         Art, fonts
```

Rules:
- `FightingGame.Simulation` targets `net8.0` (same as the Godot project) and has no Godot package reference.
- `FightingGame.csproj` includes all `.cs` files under `project/` by default. It must exclude `simulation/**` with `<Compile Remove="simulation/**" />` and use a `ProjectReference` instead.
- Tests and tools are not game files, so they are outside `project/`. They reference `FightingGame.Simulation` only.

---

## 5. Scenes (Draft)

| Scene | Purpose |
|---|---|
| `MainMenu.tscn` | Play Local, Play Online (Host / Join), Replay Viewer, Quit |
| `Lobby.tscn` | Player list, device assignment, Start (host), Back |
| `Match.tscn` | Runs a session. Contains the stage view, fighter views, HUD |
| `Stage01.tscn` | Stage authoring: terrain boxes, platform, spawn points |
| `Fighter.tscn` | Fighter view: sprite, debug box drawing |
| `ReplayViewer.tscn` | Timeline slider, play/pause/step, hash check results |

---

## 6. Open Decisions

| # | Topic | Options | Recommendation |
|---|---|---|---|
| — | None at this time | | |

---

## 7. Milestones (Draft)

Each milestone must be runnable and testable before the next one starts.

| # | Milestone | Done when |
|---|---|---|
| M0 | Project skeleton | Folder layout, test project, build command work. **Done 2026-09-25** (Godot editor build not yet verified) |
| M1 | Core math | `Fixed`, `FixedVector2`, `FixedAabb`, `Rng`, hash; unit tests pass |
| M2 | Simulation core | One fighter moves, jumps, falls, collides with a hard-coded stage. State save/restore/hash works. Boxes drawn as rectangles |
| M3 | Editor authoring | Stage authored in `Stage01.tscn` and converted to `StageData` |
| M4 | Determinism tools | Replay recording, replay viewer with slider, hash check, SyncTest mode |
| M5 | Local multiplayer | Main menu (local), lobby with device assignment, 2–4 local players |
| M6 | Combat | Frame data, hitboxes, hurtboxes, damage, health bar, death and restart. Fair resolution |
| M7 | Art and animation | Placeholder sprites driven by `Action` + `ActionFrame` |
| M8 | Rollback (offline) | `RollbackSession` with `LoopbackTransport`: two sessions in one process with simulated latency and loss. No desync |
| M9 | Online (ENet) | Host / Join, online lobby, online match over localhost/LAN. Desync detection |
| M10 | Steam | `SteamTransport` |

---

## 9. Detailed Design: M0–M2

Status: **Agreed** (2026-09-25).

### 9.1 Conventions

- Coordinates: **Y points down** (same as Godot). No axis flip between the simulation and the editor.
- Fighter `Position` = bottom-center of the collision box (the feet). Boxes are defined relative to this point.
- Tick rate: 60 Hz. All speeds are in pixels per frame. All accelerations are in pixels per frame².
- Overflow: `Fixed` arithmetic is `unchecked` (it wraps, which is deterministic in C#). Debug builds assert that values stay in a safe range.

### 9.2 Core (M1)

| Type | Kind | Content |
|---|---|---|
| `Fixed` | `readonly struct` | `long Raw`. `FromInt`, `FromRatio(num, den)`, `FromRaw`. Operators `+ - * / %`, unary `-`, comparisons. `Abs`, `Min`, `Max`, `Clamp`, `Sign`, `Floor`, `Ceil`, `Round`, `ToInt` (floor). `ToFloat()` only for presentation (marked with an attribute or a separate extension in the Godot project) |
| `FixedVector2` | `readonly struct` | `Fixed X, Y`. `+ -`, scalar `*` and `/`, `Zero` |
| `FixedAabb` | `readonly struct` | `FixedVector2 Min, Max`. `Overlaps`, `Translate`, `MirrorX(originX)`, `FromCenterSize` |
| `Rng` | `struct` | xorshift64* or PCG32 state (`ulong`). `NextInt(maxExclusive)`, `NextFixed()` |
| `StateHasher` | `ref struct` | FNV-1a 64-bit. `Add(int)`, `Add(long)`, `Add(Fixed)`, `Add(FixedVector2)` … |

Rounding rules (tested):
- Multiply: `(a.Raw * b.Raw) >> 16`. Arithmetic shift, so the result rounds toward negative infinity.
- Divide: `(a.Raw << 16) / b.Raw`. C# integer division, so the result rounds toward zero.
- `FromRatio(num, den)`: same rule as divide.

### 9.3 State layout (M2)

- `WorldState` is **one unmanaged struct**. It contains `Frame`, `Rng`, `MatchPhase`, `PhaseTimer`, and `Fighters`.
- `Fighters` is a fixed-size inline array of 4 `FighterState` (`[InlineArray(4)]`, available in .NET 8). There is no heap allocation.
- Snapshot for rollback = struct copy (`WorldState copy = state;`). The ring buffer is `WorldState[]`. This is very fast.
- Hash = explicit, field by field. Each state struct has a method `void Hash(ref StateHasher h)`.
  - Reason: a raw memory hash also reads the struct padding bytes. Padding bytes can contain different values on different machines. An explicit hash does not read them.
  - The same field list is used for the state diff tool (M4).
- Unit test: every field of `FighterState` changes the hash (this catches a field that was not added to `Hash`).

`FighterState` fields for M2:

| Field | Type | Note |
|---|---|---|
| `Active` | `bool` | Slot is used in this match |
| `Position` | `FixedVector2` | Feet |
| `Velocity` | `FixedVector2` | |
| `Facing` | `sbyte` | -1 or +1 |
| `Action` | `FighterAction` (byte enum) | Idle, Walk, JumpSquat, Airborne, Land |
| `ActionFrame` | `int` | Frames since the action started |
| `Grounded` | `bool` | |
| `DropThroughTimer` | `int` | Frames the fighter ignores one-way platforms |
| `JumpsLeft` | `byte` | Double jump. Reset to 2 on landing (ground jump + one air jump) |
| `PrevInput` | `InputBits` | Previous frame input, to detect "button pressed this frame" |

`PrevInput` is part of the state. Rule: anything the simulation remembers between frames must be in `WorldState`.

### 9.4 Static data (M2)

- `GameData` (class, read-only after load): `StageData`, `FighterStats`, later `MoveData`.
- `StageData`: `FixedAabb[] Solids`, `FixedAabb[] Platforms`, `FixedVector2[] SpawnPoints`. The array order is the order of the nodes in the scene tree, so it is the same on every machine.
- `FighterStats`: `WalkSpeed`, `AirSpeed`, `AirAcceleration`, `Gravity`, `MaxFallSpeed`, `JumpVelocity`, `JumpSquatFrames`, `CollisionBoxSize`.
- In M2 the stage is hard-coded in C#. In M3 it comes from `Stage01.tscn`.

### 9.5 Tick (M2)

```csharp
public static class Simulation
{
    public static void Tick(ref WorldState state, in FrameInput input, GameData data);
}
```

- `FrameInput` = inline array of 4 `InputBits` (`ushort` flags).
- Phases as in section 3.4. In M2 only phases 1, 2, and 6 exist.
- Phase 2 movement against terrain, for each fighter:
  1. Move on X. Find all solids that the box sweep touches. Stop at the nearest one.
  2. Move on Y. Find all solids that the sweep touches. Stop at the nearest one. For one-way platforms: block only if the fighter moves down, the feet were at or above the platform top on the previous position, and `DropThroughTimer == 0`.
  - Drop through: Down + Jump pressed while standing on a platform sets `DropThroughTimer` (and does not jump).
  3. Set `Grounded` if the fighter stands on a solid or a platform.
- Sweeps check every box, and the result is the minimum distance. So the box order does not change the result.

### 9.6 Game loop (M2, Godot side)

- `MatchRunner` (Node) owns `WorldState`, `GameData`, and a session (`LocalSession` in M2).
- In `_Process(delta)`: add `delta` to an accumulator. While the accumulator ≥ 1/60 s: poll input, run one tick. Limit to a maximum number of ticks for each render frame.
- The accumulator uses `double`. This is allowed, because it only decides **when** a tick runs, not **what** a tick does.
- `FighterView` (Node2D) reads `FighterState` each render and sets its `Position` (converted to float). `DebugDraw` draws collision boxes.
- No interpolation between ticks in the first version.

### 9.7 M0 tasks

1. Create `project/simulation/FightingGame.Simulation.csproj` (`net8.0`, no Godot) and `.gdignore`.
2. Update `FightingGame.csproj`: `<Compile Remove="simulation/**" />` and a `ProjectReference`.
3. Create `tests/FightingGame.Simulation.Tests` (xUnit).
4. Add all projects to `FightingGame.sln`. Solution configurations: `Debug`, `ExportDebug`, `ExportRelease`. The library builds `Release` in `ExportRelease`. The test project builds only in `Debug`.
5. Delete `NewScript.cs` (template file).
6. Verify: `dotnet build` and `dotnet test` pass, and the Godot editor builds the game.

---

## 8. Decision Log

| Date | Decision |
|---|---|
| 2026-09-25 | Document created |
| 2026-09-25 | Game style: Smash-like. Free-for-all, fighters face their move direction, no player-vs-player collision |
| 2026-09-25 | Max players: 4 in one match (all machines together). `MaxPlayers = 4` |
| 2026-09-25 | Restart rule: dead fighters stay out; the match restarts when one fighter is left |
| 2026-09-25 | Rollback: own implementation (no third-party rollback library) |
| 2026-09-25 | Fixed point: own `Fixed` type, Q48.16 in a `long`, 1 unit = 1 pixel |
| 2026-09-25 | Code structure: pure C# class library `FightingGame.Simulation` in `project/simulation/`; tests and tools outside `project/` |
| 2026-09-25 | Stage conversion: at runtime on load; authored values must be integer pixels |
| 2026-09-25 | Disconnect: the match stops and all players return to the lobby |
| 2026-09-25 | Knockback: fixed velocity per hitbox, no scaling; closed stage, no blast zones |
| 2026-09-25 | Double jump: yes. Platform drop-through: Down + Jump |
| 2026-09-25 | Section 9 (M0–M2 detailed design) agreed |
