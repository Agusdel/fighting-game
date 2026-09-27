# Fighting Game — Project Design Document

Status: **Draft**. This document is the source of truth for the design. Update it when a decision changes.

- Engine: Godot 4.8 (C# / .NET 8)
- Genre: 2D side-view platform fighter (Super Smash Bros. style), free-for-all, 2–4 players
- Scope: Simple prototype with a robust, deterministic foundation

---

## 0. Current Status and Next Steps

Update this section at the end of each work session.

- **Done:** M0, M1, M2 (simulation and Godot side). `Match.tscn` runs one fighter on the default stage with placeholder rectangles.
- **Git:** M2 Godot side is not committed yet.
- **Next:** M3 (stage authoring), design agreed in section 11. Step 1 (simulation side) is done. Next is step 2 (authoring scripts and `StageConverter`). The movement feel is OK for now (tune later).
- **Later:** make fighter behavior data-driven (see section 6).
- **Before the first public build:** create `scripts/export.sh` (see section 10).

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
|  WorldData, Tick, Physics  |  INetworkTransport              |
|  Combat, FrameData         |  Loopback / ENet / Steam        |
+----------------------------+---------------------------------+
| Core (pure C#): Fixed, FixedVector2, FixedAABB, FixedRng, Hash |
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
- Types built on it: `FixedVector2`, `FixedAABB`.

### 3.3 World state

- `WorldData` is a struct (or a class that holds only fixed-size struct arrays) with:
  - `Frame` (int)
  - `Rng` (`FixedRng`, deterministic PRNG)
  - `MatchPhase` and phase timer (for example: countdown, fighting, restarting)
  - `FighterData[MaxPlayers]`
- `FighterData` (unmanaged struct):
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

- Stage and hitboxes are authored in the Godot editor with custom `[Tool]` nodes (for example `TerrainBoxAuthoring`, `PlatformAuthoring`, `SpawnPositionAuthoring`). The nodes draw their boxes in the editor. (Final names in section 11.)
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
  scripts/                          build.sh, test.sh, godot-path.sh (see CLAUDE.md)
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
      Core/                         Fixed, FixedVector2, FixedAABB, FixedRng, Hash
      Simulation/                   WorldData, Tick, physics, combat, static data types
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
| `Stage01.tscn` | Stage authoring: terrain boxes, platform, spawn positions |
| `Fighter.tscn` | Fighter view: sprite, debug box drawing |
| `ReplayViewer.tscn` | Timeline slider, play/pause/step, hash check results |

---

## 6. Open Decisions

| # | Topic | Options | Recommendation |
|---|---|---|---|
| — | None at this time | | |

Planned rework (before or during M6): make fighter behavior data-driven. The M2 code uses a hard-coded `switch` on `FighterAction`. The goal is to define actions/states, animations, attack hitboxes, movement values, and input rules as data, so a new move does not need new simulation code.

---

## 7. Milestones (Draft)

Each milestone must be runnable and testable before the next one starts.

| # | Milestone | Done when |
|---|---|---|
| M0 | Project skeleton | Folder layout, test project, build command work. **Done 2026-09-25** |
| M1 | Core math | `Fixed`, `FixedVector2`, `FixedAABB`, `FixedRng`, hash; unit tests pass. **Done 2026-09-25** |
| M2 | Simulation core | One fighter moves, jumps, falls, collides with a hard-coded stage. State save/restore/hash works. Boxes drawn as rectangles. **Done 2026-09-26** (movement feel not yet checked by the user) |
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
| `Fixed` | `readonly struct` | `long Raw`. `FromInt`, `FromRatio(num, den)`, `FromRaw`, implicit conversion from `int`. Operators `+ - * / %`, unary `-`, comparisons. `Abs`, `Min`, `Max`, `Clamp`, `Sign`, `Floor`, `Ceil`, `Round`, `FloorToInt`, `CeilToInt`, `RoundToInt`. No `ToFloat` in the library: the Godot project adds it as an extension method (M2) |
| `FixedVector2` | `readonly struct` | `Fixed X, Y`. `+ -`, scalar `*` and `/`, `Zero` |
| `FixedAABB` | `readonly struct` | `FixedVector2 Min, Max`. `Overlaps` (touching edges do not overlap), `Translate`, `MirrorX(originX)`, `FromMinSize`, `FromCenterSize` |
| `FixedRng` | `struct` | xorshift64* (`ulong` state), seeded with SplitMix64. `NextULong`, `NextUInt`, `NextInt`, `NextFixed()` in [0, 1), `Hash` |
| `StateHasher` | `struct` | FNV-1a 64-bit, little-endian, field by field. `Add` for all integer types, `bool`, `Fixed`, `FixedVector2`, `FixedAABB`. Start with `new StateHasher()` |

Rounding rules (tested):
- Multiply: `(a.Raw * b.Raw) >> 16` with an exact 128-bit product (`Math.BigMul`). Arithmetic shift, so the result rounds toward negative infinity.
- Divide: `(a.Raw << 16) / b.Raw` with an exact 128-bit dividend (`Int128`). C# integer division, so the result rounds toward zero.
- There is no intermediate overflow. Only a final result outside Q48.16 wraps.
- `Round`: `floor(x + 0.5)`. Halves round toward positive infinity.
- Tests compare the RNG and the hash with reference values from an independent Python implementation and the published FNV-1a vectors.
- `FromRatio(num, den)`: same rule as divide.

### 9.3 State layout (M2)

- `WorldData` is **one unmanaged struct**. It contains `Frame`, `Rng` (`FixedRng`), `MatchPhase`, `PhaseTimer`, and `Fighters`.
- `Fighters` is a fixed-size inline array of 4 `FighterData` (`[InlineArray(4)]`, available in .NET 8). There is no heap allocation.
- Snapshot for rollback = struct copy (`WorldData copy = state;`). The ring buffer is `WorldData[]`. This is very fast.
- Hash = explicit, field by field. Each state struct has a method `void Hash(ref StateHasher h)`.
  - Reason: a raw memory hash also reads the struct padding bytes. Padding bytes can contain different values on different machines. An explicit hash does not read them.
  - The same field list is used for the state diff tool (M4).
- Unit test: every field of `FighterData` changes the hash (this catches a field that was not added to `Hash`).

`FighterData` fields for M2:

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
| `PrevInput` | `InputFlags` | Previous frame input, to detect "button pressed this frame" |

`PrevInput` is part of the state. Rule: anything the simulation remembers between frames must be in `WorldData`.

### 9.4 Static data (M2)

- `GameData` (class, read-only after load): `StageData`, `FighterStats`, later `MoveData`.
- `StageData`: `FixedAABB[] Solids`, `FixedAABB[] Platforms`, `FixedVector2[] SpawnPoints`. The array order is the order of the nodes in the scene tree, so it is the same on every machine. (M3 replaced `SpawnPoints` with `SingleSpawnPositions` and `SpawnPositionPairs`, see section 11.)
- `FighterStats`: `CollisionBoxSize`, `WalkSpeed`, `AirSpeed`, `AirAcceleration`, `AirFriction`, `Gravity`, `MaxFallSpeed`, `JumpVelocity`, `JumpSquatFrames`, `LandFrames`, `MaxJumps`, `DropThroughFrames`.
- `StageData` also computes `Bounds` (the box around all solids and platforms). Facing at the spawn position: toward the center of `Bounds`.
- In M2 the stage is hard-coded in C# (`DefaultGameData`): a closed 1152 x 648 box (the default Godot window size), floor top at y = 600, one platform x 426–726 at y = 420. In M3 it comes from `Stage01.tscn`.
- Default fighter: box 48 x 96, walk 5, air speed 4.5, gravity 0.6, max fall 12, jump velocity -13 (about 140 px high), jump squat 3 frames, land 3 frames.

### 9.5 Tick (M2)

```csharp
public static class Simulator
{
    public static void Tick(ref WorldData state, in FrameInput input, GameData data);
}
```

- The class is `Simulator`, not `Simulation`, because the namespace is `FightingGame.Simulation`.

- `FrameInput` = inline array of 4 `InputFlags` (`ushort` flags).
- Phases as in section 3.4. In M2 only phases 1, 2, and 6 exist.
- Phase 2 movement against terrain, for each fighter:
  1. Move on X. Find all solids that the box sweep touches. Stop at the nearest one.
  2. Move on Y. Find all solids that the sweep touches. Stop at the nearest one. For one-way platforms: block only if the fighter moves down, the feet were at or above the platform top on the previous position, and `DropThroughTimer == 0`.
  - Drop through: Down + Jump pressed while standing on a platform sets `DropThroughTimer` (and does not jump).
  3. `Grounded` = a downward move was blocked.
- Sweeps check every box, and the result is the minimum distance. So the box order does not change the result.
- Gravity is applied every frame, also on the ground. The downward sweep stops it and sets `Grounded`. So walking off an edge needs no special case.
- `ActionFrame` is incremented at the start of phase 1. A new action starts at `ActionFrame = 0`.
- Action rules:
  - `Idle`/`Walk`: Jump pressed → `JumpSquat` (or drop-through with Down on a platform). Walk sets the speed directly (no ground acceleration).
  - `JumpSquat`: after `JumpSquatFrames` → jump.
  - `Airborne`: air control with acceleration and friction. Jump pressed with `JumpsLeft > 0` → air jump. Facing does not change in the air.
  - Landing → `Land` (no input for `LandFrames`), and `JumpsLeft = MaxJumps`.
  - Leaving the ground without a jump (walk off, drop-through) → `Airborne` with `JumpsLeft = MaxJumps - 1`.
- `MatchPhase` has only `Fighting` in M2.

### 9.6 Game loop (M2, Godot side)

- `MatchRunner` (Node2D, root of `Match.tscn`) owns `WorldData` and `GameData`. There is no session class yet: M2 calls `Simulator.Tick` directly. `LocalSession` comes with M4/M5.
- In `_Process(delta)`: add `delta` to an accumulator. While the accumulator ≥ 1/60 s: poll input, run one tick. Limit to a maximum number of ticks for each render frame.
- The accumulator uses `double`. This is allowed, because it only decides **when** a tick runs, not **what** a tick does.
- After the ticks of a render frame, `MatchRunner` calls `Refresh` on each view. The views do not read the state by themselves, so the `_Process` order does not matter.
- `FighterView` (Node2D, origin = feet) draws the collision box, a facing triangle, and a feet marker (filled = grounded). `MatchRunner` creates one `FighterView` per slot in code, under the `Fighters` node.
- `StageView` draws solids and platforms. `DebugLabel` shows the frame, the state hash, and the fighter 0 state.
- Keyboard input: `KeyboardInputMap` reads **physical** keys directly (`Input.IsPhysicalKeyPressed`), not Godot input actions. Physical keys work on every layout, and two players can share one keyboard. Maps: `Wasd` (WASD, Space jump, J attack) and `Arrows` (arrows, keypad Enter jump, keypad 0 attack).
- `Fixed` to float conversions are extension methods in `project/src/Presentation/FixedExtensions.cs`, so the simulation library cannot call them.
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
| 2026-09-25 | Renamed `FixedAabb` to `FixedAABB` and `Rng` to `FixedRng` |
| 2026-09-26 | Renamed `InputBits` to `InputFlags` |
| 2026-09-26 | Keyboard input reads physical keys directly (no Godot input actions) |
| 2026-09-26 | `scripts/build.sh` skips the headless Godot build while the editor is open (the MCP plugin changes `project.godot` when a headless editor quits) |
| 2026-09-25 | Build with scripts in `scripts/` (dotnet build + Godot headless build). No editor MCP for now |
| 2026-09-26 | Godot MCP Pro added (`mcp/` server, `project/addons/godot_mcp/` plugin). Claude edits scenes and Godot resources only through the MCP. This replaces "No editor MCP for now" |
| 2026-09-26 | Builds that leave the developer's computer are made only with `scripts/export.sh`, which removes the MCP addon and autoloads and checks the result (section 10) |
| 2026-09-27 | M3 stage authoring design agreed (section 11): component nodes on a snapping `StageNode` base, spawn position pairs + single spawn positions, stages inherit `StageBase.tscn` |
| 2026-09-27 | Renames: `WorldState` → `WorldData`, `FighterState` → `FighterData` (Data suffix for simulation data types; `FighterStats`, `FrameInput`, and enums keep their names). `SpawnSelector` → `SpawnPositionSelector`, `SpawnPair` → `SpawnPositionPair`, authoring `SpawnPoint` → `SpawnPosition`. "Spawn position" is the term everywhere |
| 2026-09-27 | A stage has one or more single spawn positions (`SingleSpawnPositions`); an odd player count uses one at random |

---

## 10. Task: Release export without the MCP

Status: **Not started.** Do this before the first build that leaves the developer's computer (itch.io, Steam, or a build for friends).

### Goal

A release build must not contain any part of the Godot MCP: no `addons/godot_mcp/` files, no `MCP*` autoloads, and no plugin entry. The developer must not need to remember manual steps.

### Why a manual export is not safe

- The plugin adds three autoloads (`MCPScreenshot`, `MCPInputService`, `MCPGameInspector`) to `project.godot` when the editor starts. It removes them when the editor closes. An export from the open editor always includes them.
- If the editor crashes, the autoloads stay in `project.godot`.
- An export preset filter can exclude `addons/godot_mcp/*`, but not the autoloads. The game then shows "Can't autoload" errors at start.
- Other AI files (`mcp/`, `.claude/`, `.mcp.json`) are outside `project/`, so Godot never exports them. The C# assemblies do not reference the addon.

### Task: create `scripts/export.sh`

Usage: `scripts/export.sh <preset> <output path>`. The script does these steps:

1. Copy the committed `project/` folder (`git archive HEAD`) to a temporary folder. Local and untracked files are not included.
2. Delete `addons/godot_mcp/` from the copy.
3. In the copy of `project.godot`, remove the `MCP*` autoload lines and the `godot_mcp` entry in `[editor_plugins]`. This changes only the temporary copy.
4. Run `godot --headless --export-release <preset> <output path>` in the copy. Use `scripts/godot-path.sh` to find Godot. The plugin does nothing in a command-line export, so it cannot add the autoloads again.
5. Check the exported files (`.pck`, executable, and the .NET data folder). Search for `godot_mcp`, `MCPScreenshot`, `MCPInputService`, and `MCPGameInspector`. If one is found, delete the output and stop with an error.
6. Delete the temporary folder.

Also:

- Add `addons/godot_mcp/*` to the exclude filter of each export preset. This is a second protection for exports from the editor.
- Test the script with a real export preset. Start the exported game and confirm that there are no autoload errors.
- Add `export.sh` to the `scripts/` list in `CLAUDE.md` and in section 4.

### Rule

Use `scripts/export.sh` for all builds that leave the developer's computer. Use the editor Export button only for local tests.

---

## 11. Detailed Design: M3 Stage Authoring

Status: **Agreed** (2026-09-27).

### 11.1 Goals

- Build stages in the Godot editor: place reusable pieces (platforms, blocks), add art, and add collision boxes and spawn positions.
- See every collision box and spawn position in the editor while you edit.
- Convert the authored nodes into `StageData` with exact, deterministic values.
- Do not use Godot physics or Godot collision shapes.

### 11.2 Scripts (components)

Stage objects are built from small components. A structure combines one or more collision components with any number of visual nodes. More component types (for example slopes or circles) can come later.

All scripts are C# `[Tool]` scripts in `project/src/Authoring/`. They have no static state (C# tool scripts must let the editor unload the assembly on rebuild).

| Script | Base | Exports | Purpose and editor drawing |
|---|---|---|---|
| `StageNode` | `Node2D` | none | Base class for all authoring nodes. Snaps `Position` to whole pixels and resets rotation, scale, and skew (see 11.4). Not abstract: attach it directly to group nodes (for example `Structures`, `SpawnPositions`). |
| `StageRoot` | `StageNode` | none | Root of a stage scene. Shows configuration warnings (see 11.6). |
| `StageStructure` | `StageNode` | none | Groups the components and visuals of one object (a platform, a wall, a complex block). Draws nothing. |
| `StageCollisionBox` | `StageNode` | `Vector2I Size`, `StageCollisionType Type` (`Solid`, `Platform`) | One collision box. Filled rectangle + outline, color by type (for example gray for `Solid`, orange for `Platform`). |
| `SpawnPositionPair` | `StageNode` | none | Groups two spawn positions. A dashed line between its two `SpawnPosition` children. |
| `SpawnPosition` | `StageNode` | none | One spawn position. A feet marker and an outline of the fighter collision box (from `DefaultGameData.CreateFighterStats()`). A different color for a single spawn position (not in a pair). |

Example structure:

```
StageStructure            (Node2D, position in whole pixels)
├── StageCollisionBox     (Size, Type = Solid)
├── StageCollisionBox     (a structure can have more than one)
└── Sprite2D              (visuals: no rules, any float position/rotation/scale)
```

Conventions:
- `StageCollisionBox`: the node `Position` is the **top-left corner**. `Min = Position`, `Max = Position + Size`. `Size` must be at least 1 x 1.
- `SpawnPosition`: the node `Position` is the fighter **feet** (bottom-center of the collision box).
- A `StageCollisionBox` can be at any depth under `StageRoot` (usually in a `StageStructure`). The converter finds it anywhere.
- Every `Node2D` between an authoring node and `StageRoot` must also have whole-pixel positions and no rotation, scale, or skew. Use `StageStructure` (or other `StageNode` types) for grouping, so the editor snaps them.
- `StageCollisionType` is used by the converter only. The simulation keeps separate arrays (`Solids`, `Platforms`).

### 11.3 Spawn positions

- A `SpawnPositionPair` node has exactly **two** `SpawnPosition` children. The two positions are "opposite" positions. They do not need to be exact mirrors.
- A `SpawnPosition` that is **not** a child of a `SpawnPositionPair` is a **single spawn position**. A stage has at least **one** single spawn position (it can have more).
- A stage has at least `MaxPlayers / 2` pairs (2 pairs for 4 players).

Spawn position selection at match start (`SpawnPositionSelector`, in the simulation, with `WorldData.Rng`, so it is deterministic):
1. `pairsNeeded = playerCount / 2`. Pick `pairsNeeded` different pairs at random (partial Fisher-Yates shuffle of the pair indices).
2. Make a list of the positions of the picked pairs. If `playerCount` is odd, add one single spawn position, chosen at random.
3. Shuffle the list at random, and give position `i` to active player slot `i`.

Examples: 1 player → 1 random single. 2 players → 1 random pair. 3 players → 1 random pair + 1 random single. 4 players → 2 random pairs.
Facing at the spawn position: toward the center of the stage `Bounds` (no change).

The match restart (M6) runs the same selection again. `Rng` has advanced, so the spawn positions are different.

### 11.4 Exact values (integer pixels)

Godot stores positions as 32-bit floats (about 24 bits of precision). Most fixed-point values with a fraction cannot be stored exactly in a float, but all integers up to 16 million can. So all authored values are **whole pixels**. There are three levels of protection:

1. **Integer types**: `StageCollisionBox.Size` is a `Vector2I`. The Inspector accepts only whole numbers.
2. **Editor snapping**: the `StageNode` base class calls `SetNotifyLocalTransform(true)`. On `NotificationLocalTransformChanged`, it rounds `Position` to whole pixels and resets rotation, scale, and skew to their default values. A drag moves in 1-pixel steps.
3. **Check at load**: the converter does not trust the editor (a `.tscn` file can be changed by hand, and a parent node can move by a fraction).

Converter rules:
- For each authored node, add the local `Position` of the node and of every ancestor up to `StageRoot`, with `int` math. Do not use `GlobalPosition` (it comes from float matrix math).
- Each local value: `int v = Mathf.RoundToInt(f)`. If `Math.Abs(f - v) > 0.001`, report an error with the node path.
- If the node or an ancestor has rotation, a scale other than (1, 1), or skew, report an error with the node path.
- Collect all errors, then fail with the full list (not only the first error).

### 11.5 Converter and data flow

- `StageConverter` (in `project/src/Authoring/`): `StageData Convert(StageRoot root)`. It walks the tree depth-first in child order, and collects `StageCollisionBox`, `SpawnPositionPair`, and `SpawnPosition` nodes at any depth. The child order is saved in the `.tscn` file, so the result is the same on every machine.
- Simulation changes (`project/simulation/`):
  - `StageData(FixedAABB[] solids, FixedAABB[] platforms, FixedVector2[] singleSpawnPositions, SpawnPositionPair[] spawnPositionPairs)`. `SpawnPositionPair` is a struct with two `FixedVector2` (`A`, `B`). This replaces `SpawnPoints`. The simulation type `SpawnPositionPair` is in namespace `FightingGame.Simulation`; the authoring node is `FightingGame.Authoring.SpawnPositionPair`.
  - `StageData.ComputeHash()`: hash of all boxes and spawn positions. In M9, peers compare it when they connect.
  - `WorldData.Create` uses the spawn position selection in 11.3.
  - The hard-coded stage moves from `DefaultGameData` to the test project (`TestStages`). `DefaultGameData` keeps only the fighter stats.
- Unit tests: spawn position selection (counts, no duplicate positions, deterministic for a seed, both positions of a pair used, all singles used over many seeds), `StageData` validation, hash.

### 11.6 Editor feedback

- `StageRoot` and `SpawnPositionPair` implement `_GetConfigurationWarnings()`. The scene tree shows a warning icon when the structure is wrong (for example: no single spawn position, a `SpawnPositionPair` without exactly two `SpawnPosition` children, too few pairs, a non-integer position). The warnings use the same checks as the converter.
- Authoring scripts call `UpdateConfigurationWarnings()` on the `StageRoot` when they change.

### 11.7 Scenes

| Scene | Content |
|---|---|
| `scenes/stages/pieces/StageSimpleStructure.tscn` | `StageStructure` + one `StageCollisionBox` (`Type = Solid`) + `Sprite2D` |
| `scenes/stages/pieces/StageSimplePlatform.tscn` | `StageStructure` + one `StageCollisionBox` (`Type = Platform`) + `Sprite2D` |
| `scenes/stages/StageBase.tscn` | Base scene for all stages (see below) |
| `scenes/stages/Stage01.tscn` | **Inherits** `StageBase.tscn`. Adds floor, ceiling, and walls (`StageStructure` nodes built in the stage) and one platform under `Structures`, and one or more single `SpawnPosition` nodes and 2–3 `SpawnPositionPair` nodes under `SpawnPositions` |
| `scenes/Match.tscn` | `MatchRunner` gets `[Export] PackedScene StageScene`. At `_Ready`, it instantiates the stage, converts it, and creates `GameData`. `StageView` becomes a debug overlay (on/off with a key, off by default) |

Stage01 recreates the M2 stage: 1152 x 648, floor top at y = 600, platform x 426–726 at y = 420.

Base scene: every stage scene inherits `StageBase.tscn`, so all stages have the same root nodes. To add a root node to all stages, add it to `StageBase.tscn`.

```
Stage (StageRoot)              root
├── Background (Node2D)        visuals only (no rules)
├── Structures (StageNode)     StageStructure nodes
└── SpawnPositions (StageNode) single SpawnPosition nodes and SpawnPositionPair nodes
```

`StageBase.tscn` alone shows configuration warnings (no spawn positions). This is expected: it is never used as a stage.
Done by the user: `StageBase.tscn` (root `Node2D` named `Stage`, no script yet) and `Stage01.tscn` (inherits `StageBase.tscn`). Step 3 attaches `StageRoot` to the root and adds the child nodes.

Prefab scenes and sizes:
- You can add new children to a prefab instance without "Editable Children". But to change a node inside the instance (for example the `Size` of its box), you must enable "Editable Children".
- So use prefab scenes for pieces with a **fixed size** (the box matches the art).
- Build pieces with a **free size** (floor, walls, ceiling) directly in the stage from `StageStructure` + `StageCollisionBox`. Duplicate them with Ctrl+D.

### 11.8 Implementation steps

Each step stops for review.

1. Simulation: `StageData` with spawn position pairs and single spawn positions, `SpawnPositionSelector`, `ComputeHash`, tests. (Done. `TestStages` moves to step 4.)
2. Authoring scripts: `StageNode`, `StageRoot`, `StageStructure`, `StageCollisionBox`, `SpawnPositionPair`, `SpawnPosition` (drawing and snapping), and `StageConverter` with its checks.
3. Scenes: `StageBase.tscn`, `Stage01.tscn` (inherited), `StageSimpleStructure.tscn`, `StageSimplePlatform.tscn` (built with the Godot MCP).
4. `MatchRunner` loads `Stage01.tscn`. Debug overlay toggle. Test in the running game.

### 11.9 Not in M3

- Resize handles in the viewport (needs an `EditorPlugin`). The first version uses the `Size` property in the Inspector.
- Moving platforms or other stage state that changes during a match.
- Other collision shapes (slopes, circles). The component design allows them, but each one also needs new fixed-point collision code in the simulation.
