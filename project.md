# Fighting Game — Project Design Document

Status: **Draft**. This document is the source of truth for the design. Update it when a decision changes.

- Engine: Godot 4.8 (C# / .NET 8)
- Genre: 2D side-view platform fighter (Super Smash Bros. style), free-for-all, 2–4 players
- Scope: Simple prototype with a robust, deterministic foundation

---

## 0. Current Status and Next Steps

Update this section at the end of each work session.

- **Done:** M0–M5. The game starts at the main menu (`Main.tscn`): Play Local → lobby (players join with their keyboard set or controller) → match → Esc → lobby. Up to 4 fighters on `Stage01.tscn` with the data-driven state machine, two attacks with direction variants, hitstop, defeat, and round restart. Debug drawing: F1 stage boxes, F2 hurtboxes, hitboxes, and state progress bar. HUD health bars.
- **Open in M3:** the piece scenes `StageSimpleStructure.tscn` and `StageSimplePlatform.tscn` wait for art. When art exists, turn off `ShowStageDebug` and `ShowCombatDebug` on the `Match` node.
- **Next:** M6 (rollback, offline). It starts with a SyncTest mode (section 7). Design first, with the user. Topics to decide: SyncTest session (roll back N frames every frame, compare hashes), input queue and prediction, snapshot ring buffer, `RollbackSession` behind `IMatchSession`, `LoopbackTransport` with simulated latency and loss, what the views do after a rollback.
- **Small items for later:** input buffer (a press on the last frame of a busy state is lost), input latch (a tap shorter than one tick is lost, 13.2), pause menu (Esc / controller Start; now it goes back to the lobby), review the `Data` names (section 12.3), tune movement and attack values (better when fighter states can be authored in the editor, M10).
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

Done in M4 (section 12): fighter behavior is data-driven. The M2 code uses a hard-coded `switch` on `FighterAction`. The goal is to define actions/states, animations, attack hitboxes, movement values, and input rules as data, so a new move does not need new simulation code.

---

## 7. Milestones (Draft)

Each milestone must be runnable and testable before the next one starts.

| # | Milestone | Done when |
|---|---|---|
| M0 | Project skeleton | Folder layout, test project, build command work. **Done 2026-09-25** |
| M1 | Core math | `Fixed`, `FixedVector2`, `FixedAABB`, `FixedRng`, hash; unit tests pass. **Done 2026-09-25** |
| M2 | Simulation core | One fighter moves, jumps, falls, collides with a hard-coded stage. State save/restore/hash works. Boxes drawn as rectangles. **Done 2026-09-26** (movement feel not yet checked by the user) |
| M3 | Editor authoring | Stage authored in `Stage01.tscn` and converted to `StageData`. **Done 2026-09-27** (piece scenes wait for art) |
| M4 | Combat and fighter state machine (**Done 2026-09-28**) | Data-driven fighter states, two attacks with direction variants, hitboxes, hurtboxes, damage, hitstun, knockback, hitstop (can be disabled), health bars, death and restart. Fair resolution (section 12) |
| M5 | Local multiplayer (**Done 2026-09-30**) | Main menu (local), lobby with device assignment, 2–4 local players |
| M6 | Rollback (offline) | First step: SyncTest mode. Then `RollbackSession` with `LoopbackTransport`: two sessions in one process with simulated latency and loss. No desync |
| M7 | Online (ENet) | Host / Join, online lobby, online match over localhost/LAN. Desync detection |
| M8 | Steam | `SteamTransport` |
| M9 | Determinism tools | Replay recording, replay viewer with slider, hash check, state diff. Only when needed (the unit tests already check determinism with hashes) |
| M10 | Art, animation, and fighter authoring | Sprites and `AnimationPlayer` driven by `StateId` + `StateFrame`, VFX and sound timelines, fighter states authored in the editor with a frame viewer, stage piece scenes |

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
  - The same field list is used for the state diff tool (M9).
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
| 2026-09-27 | M3 done: `Match.tscn` loads its stage from a stage scene (`StageScene` export). The stage hash of `Stage01.tscn` equals the hash of the M2 stage |
| 2026-09-28 | New milestone order: M4 combat and fighter state machine, M5 local multiplayer, M6 rollback (starts with SyncTest), M7 online (ENet), M8 Steam, M9 determinism tools (when needed), M10 art, animation, and fighter authoring |
| 2026-09-28 | M4 design agreed (section 12). Static fighter definition: `FighterDefinitionData` (names to review later). Shared transitions of `Actionable` states have priority over the state's own transitions. Down attack variant only in the air or on a platform |
| 2026-09-28 | Fighters can turn in the air: `AirControl` (and `Free` in the air) sets the facing when the state has `CanTurn`. `Jump`, `DoubleJump`, and `Fall` have `CanTurn`. Attacks, `Locked`, and `Knockback` never turn. (This replaces the M2 rule "facing does not change in the air".) |
| 2026-09-30 | M5 design agreed (section 13): input from InputMap actions (controller template copied per controller), `IMatchSession` / `LocalSession`, app root `Main.tscn` with `MatchSetup`, Smash-style lobby with `LobbyPlayerSlot`, scene folders per part of the game |

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

The match restart (M4) runs the same selection again. `Rng` has advanced, so the spawn positions are different.

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
  - `StageData.ComputeHash()`: hash of all boxes and spawn positions. In M7, peers compare it when they connect.
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
2. Authoring scripts: `StageNode`, `StageRoot`, `StageStructure`, `StageCollisionBox`, `SpawnPositionPair`, `SpawnPosition` (drawing and snapping), and `StageConverter` with its checks. (Done. Tested in the editor with an in-memory node tree: snapping, `Size` clamp, pair warning, root warnings with node paths. Sprites with float values are ignored.)
   - All scripts have `[GlobalClass]`, so they appear by name in the "Add Node" dialog.
   - `StageCollisionBox`, `SpawnPosition`, and `SpawnPositionPair` draw only in the editor. In the game, the art comes from visual nodes, and `StageView` is the debug overlay.
   - Only the chain between an authoring node (`StageCollisionBox`, `SpawnPosition`) and the root is checked. A non-`Node2D` node in that chain (for example `Node` or `CanvasLayer`) is an error, because it breaks the transform chain.
   - The root position is not included. Positions are relative to the stage root.
3. Scenes: `StageBase.tscn`, `Stage01.tscn` (inherited), `StageSimpleStructure.tscn`, `StageSimplePlatform.tscn` (built with the Godot MCP). (`StageBase.tscn` and `Stage01.tscn` done. `Stage01` has the same boxes and spawn positions as the M2 stage, in the same order: `Floor`, `Ceiling`, `LeftWall`, `RightWall`, `Platform`; `Single`; `PairOuter`, `PairInner`. The piece scenes wait for art.)
4. `MatchRunner` loads `Stage01.tscn`. Debug overlay toggle. Test in the running game. (Done.)
   - `MatchRunner` exports `StageScene` (a `PackedScene` with a `StageRoot` root). It adds the stage as its first child at the origin, converts it, and prints the stage hash. On a conversion error, it logs all errors and stops.
   - `ShowStageDebug` export + F1 key show or hide the `StageView` overlay. It is on in `Match.tscn` until the stage has art.
   - The hard-coded stage moved to `tests/.../TestStages.cs` (a copy of `Stage01.tscn`). `DefaultGameData` keeps only `CreateFighterStats()`.
   - Check: `Stage01.tscn` in the game and `TestStages.CreateDefault()` give the same `StageData.ComputeHash()` (`CAE0F8780BBCE087`). So the editor conversion is exact.

### 11.9 Not in M3

- Resize handles in the viewport (needs an `EditorPlugin`). The first version uses the `Size` property in the Inspector.
- Moving platforms or other stage state that changes during a match.
- Other collision shapes (slopes, circles). The component design allows them, but each one also needs new fixed-point collision code in the simulation.

---

## 12. Detailed Design: M4 Combat and Fighter State Machine

Status: **Agreed** (2026-09-28).

### 12.1 Goals

- Define any number of fighter states as data, with rules for the transitions between them.
- Code hooks (`OnEnter`, `OnUpdate`, `OnExit`) for logic that data cannot express.
- Two attacks with direction variants, hitboxes on selected frames, damage, hitstun, knockback, and hitstop.
- Everything stays deterministic and rollback-safe.
- The design must allow later: more attack buttons, charged attacks, projectiles, blocking, grabs, editor authoring, art.

### 12.2 Rollback rule: a state is two numbers

The current state of a fighter is not an object. It is two values in `FighterData`:

- `StateId` (`ushort`): the index of the state in the character data.
- `StateFrame` (`int`): frames since the state started (0 on the first frame).

The state definitions are static data (like `StageData`): shared by all fighters, never changed during a match, not saved on rollback. A rollback restores `StateId` and `StateFrame`, and the fighter is again in the correct state, on the correct frame.

`StateId` and `StateFrame` replace `Action` and `ActionFrame`. The `FighterAction` enum is removed.

### 12.3 Static data types

Names follow the `Data` suffix rule. Review the names later: `State` for mutable data and `Data` for static data was clearer, but `State` now means a state of the state machine.

| Type | Content |
|---|---|
| `FighterDefinitionData` | The static definition of one playable character: `FighterStats`, `FighterStateData[] States`, shared transitions (12.5), default hurtboxes, and the ids of the states that the system needs (`Idle`, `Fall`, `Hitstun`, `Dead`, ...). `FighterData` is one fighter in a match; `FighterDefinitionData` is what it is made of. |
| `FighterStateData` | One state. See below. |
| `HitboxData` | Box (relative to the feet, defined for `Facing = +1`, mirrored for `-1`), active frames (from, to), damage, hitstun frames, knockback (velocity, facing-relative), hitstop frames. |
| `HurtboxData` | Box (relative to the feet, mirrored by facing), active frames. |
| `TransitionData` | Conditions (all must be true), target state, active frames (a "cancel window"). |
| `ConditionData` | One condition: a type (enum) and a parameter. |
| `FrameActionData` | An action at one frame, for example "set velocity (8, 0)", facing-relative. |
| `MatchRulesData` | Match settings: `HitstopEnabled`, `RestartDelayFrames`. Part of `GameData`, so all peers use the same rules. |

`GameData` gets `FighterDefinitionData Character` (in place of `FighterStats Fighter`) and `MatchRulesData Rules`.

`FighterStateData`:

```csharp
public sealed class FighterStateData
{
    public string Name;                       // "Attack1Up": key for the animation, VFX, and sound
    public int Duration;                      // 0 = no end (Idle, Walk, Fall)
    public ushort NextState;                  // state after Duration ends
    public MovementMode Movement;             // see 12.6
    public StateFlags Flags;                  // Actionable, CanTurn, ArmoredAgainstHits, ...
    public ushort? OnLanding;                 // target when the fighter lands (null = stay in this state)
    public ushort? OnLeaveGround;             // target when the fighter leaves the ground without a jump (null = stay)
    public HitboxData[] Hitboxes;
    public HurtboxData[] Hurtboxes;           // empty = use the character default hurtboxes
    public FrameActionData[] FrameActions;
    public TransitionData[] Transitions;      // checked in order; the first match wins
    public StateHook? OnEnter, OnUpdate, OnExit;
}
```

States are built in C# with a small builder (code-first, see 12.11). The builder references states by name and resolves the names to ids.

### 12.4 Hooks

```csharp
public delegate void StateHook(ref FighterData fighter, in StateContext context);
public delegate bool StateCondition(in FighterData fighter, in StateContext context);
// StateContext: input of this frame, FighterDefinitionData, StageData, MatchRulesData.
```

Rules for hooks (they keep the simulation deterministic):
- A hook is a static method in the simulation library.
- It reads and writes only the `FighterData` that it receives, and reads only static data.
- It captures no mutable variables. It uses only fixed-point math.

Data first, hooks only when data is not enough. `ConditionData` has a `Custom` type that calls a `StateCondition` for special conditions.

`FighterData` gets 4 generic values for hooks: `StateVar0` to `StateVar3` (`int`), reset to 0 when a state starts. Example: a charged attack stores the charge time in `StateVar0`. So new mechanics do not need new `FighterData` fields.

### 12.5 Transitions

"Actionable" means: the fighter is free to start a new action (jump, attack). `Idle`, `Walk`, `Jump`, `DoubleJump`, and `Fall` are actionable. Busy states (`JumpSquat`, `Land`, attacks, `Hitstun`, `Dead`) are not.

Order of checks in the state update (each fighter, phase 1):
1. **Duration end:** if `Duration > 0` and `StateFrame >= Duration`, go to `NextState`.
2. **Shared transitions:** if the (new) state has the flag `Actionable`, the shared ground list (if grounded) or air list (if not grounded) of the fighter definition. Example: "Jump pressed → `JumpSquat`", "Attack1 pressed + Up held → `Attack1Up`". So common rules are written once.
3. **State transitions:** the `Transitions` of the current state, in order.

The first transition with all conditions true (and inside its frame window) wins. Shared rules come before the state rules, so an action (jump, attack) has priority over a simple movement change (`Idle` → `Walk`) on the same frame. At most one transition happens per tick: the duration end, or else one rule transition. (Reason: after `JumpSquat` ends, `Grounded` is still true from the last collision, so the new `Jump` state would use the ground list.) A button pressed on exactly the frame when a timed state ends is not seen; an input buffer can fix this later. A transition to the current state restarts it.

Condition types (first version): `InputPressed(button)`, `InputHeld(button)`, `InputReleased(button)`, `DirectionHeld(Up | Down | Forward | Back | None)`, `Grounded`, `Airborne`, `OnPlatform`, `VelocityYDown`, `JumpsLeft`, `Custom`. `InputReleased` and `InputHeld` allow charged attacks later.

Ground and air changes are not transitions in the list: they happen in the movement phase (after the collision), with `OnLanding` and `OnLeaveGround`. So a landing changes the state on the same frame (the M2 behavior).

Being hit is not a state rule either. The hit phase forces the change to `Hitstun` (12.8), unless the state has the flag `ArmoredAgainstHits`. So no state can forget it.

### 12.6 Movement modes

| Mode | Effect |
|---|---|
| `GroundControl` | Walk input sets the X speed (M2 walk). |
| `AirControl` | Air acceleration and friction (M2 air control). |
| `Free` | `GroundControl` when grounded, `AirControl` when airborne. For states that work in both places (`Attack1*`). |
| `Locked` | No control. On the ground the X speed goes to 0. In the air the fighter keeps its momentum. |
| `Knockback` | No control. Friction only (ground friction or air friction). For `Hitstun`. |

Gravity applies in all modes (a future flag can switch it off, for example for a hover).

### 12.7 Fighter states (first version)

| State | Duration | Movement | Notes |
|---|---|---|---|
| `Idle` | 0 | `GroundControl` | Actionable. Direction → `Walk`. `OnLeaveGround` → `Fall`. |
| `Walk` | 0 | `GroundControl` | Actionable. No direction → `Idle`. `OnLeaveGround` → `Fall`. |
| `JumpSquat` | 3 | `Locked` | → `Jump` (with the jump velocity, one jump used). Down + Jump on a platform: drop-through (as in M2). |
| `Jump` | 0 | `AirControl` | Actionable (air). `VelocityYDown` → `Fall`. `OnLanding` → `Land`. |
| `DoubleJump` | 0 | `AirControl` | Actionable (air). Same as `Jump` (own state for its own animation). |
| `Fall` | 0 | `AirControl` | Actionable (air). `OnLanding` → `Land`. |
| `Land` | 3 | `Locked` | → `Idle`. |
| `Attack1Forward/Up/Down` | short (for example 18) | `Free` | Light attack: fast, weak. Hitbox in front, above, or below. |
| `Attack2Forward/Up/Down` | long (for example 36) | `Locked` | Heavy attack: slow, strong. Hitbox in front, above, or below. |
| `Hitstun` | from the hit | `Knockback` | → `Idle` or `Fall` when it ends. |
| `Dead` | 0 | `Locked` | No hurtbox. No transitions. |

Attack rules:
- Direction when the attack starts: Up held → `Up` variant; Down held **in the air or on a platform** → `Down` variant; otherwise `Forward`. Up and Down together → `Forward`.
- Down on solid ground gives the `Forward` variant (there is no crouch or low attack yet).
- All attack states work on the ground and in the air. A landing during an attack does not end it (`OnLanding` is null). A fall from a platform does not end it (`OnLeaveGround` is null).
- `CanTurn` is off during attacks: the fighter can walk backward during `Attack1*`, but it keeps its facing, so the hitbox does not flip.
- Cancel windows (for example `Attack1` → `Attack2`) are possible with transitions, but not in the first version.

Inputs: `InputFlags.Attack` is replaced by `Attack1` and `Attack2`. Keyboard maps: `Wasd`: J = `Attack1`, K = `Attack2`. `Arrows`: keypad 0 = `Jump`, keypad 1 = `Attack1`, keypad 2 = `Attack2`. `InputFlags` has 10 free bits for more buttons.

### 12.8 Combat

`FighterData` new fields: `Health`, `HitstopFrames`, `HitstunFrames` (the duration of the current hitstun), `HitTargets` (bitmask of fighters hit by the current attack), `StateVar0`–`StateVar3`.

`FighterStats` new field: `MaxHealth` (for example 100).

Hit detection (phase 3), for every pair (attacker, target), attacker ≠ target:
- Active hitboxes of the attacker on its current `StateFrame`, mirrored by its facing.
- Active hurtboxes of the target (the state hurtboxes, or the character default).
- A target that is `Dead`, or already in the attacker's `HitTargets`, cannot be hit.
- If more than one hitbox of the attacker overlaps, the first hitbox in the array wins.
- The result is a hit list. Nothing changes in this phase.

Hit resolution (phase 4), for each hit in the list, all at the same time:
- Target: `Health -= Damage`. `Velocity = Knockback` (X mirrored by the attacker facing). Change to `Hitstun` with `HitstunFrames`, unless the state is `ArmoredAgainstHits` (then only damage).
- Attacker: add the target to `HitTargets`.
- Hitstop: attacker and target get `HitstopFrames = max(current, hitbox value)`, if `MatchRulesData.HitstopEnabled`.
- Two fighters can hit each other on the same frame (a trade): both hits apply.
- If `Health <= 0`: change to `Dead`.

Hitstop:
- While `HitstopFrames > 0`, the fighter does not move, its `StateFrame` does not advance, and its transitions do not run. `HitstopFrames` counts down by 1 per frame.
- `PrevInput` is not updated during hitstop. So a button pressed during hitstop is still detected as "pressed" on the first frame after it (a small input buffer).
- `MatchRulesData.HitstopEnabled = false` switches it off.

`HitTargets` resets to 0 when a new state starts. So one attack hits each target once. A multi-hit attack later can reset it with a frame action.

### 12.9 Match rules (phase 5)

- `MatchPhase`: `Fighting` → `RoundOver` → (restart) `Fighting`.
- `Fighting` → `RoundOver`: when at most one fighter is alive (with 2 or more players). With 1 player, the round ends only if that fighter dies (not possible yet).
- `RoundOver` lasts `RestartDelayFrames` (for example 120). Fighters keep running their states, so the winner can move.
- Restart: all active fighters get full health and `Idle`, new spawn positions from `SpawnPositionSelector` (the `Rng` has advanced, so the positions change). `Frame` does not reset (it always increases, for rollback and replays). `WorldData` gets a `Round` counter.

### 12.10 Presentation

The presentation reads the state and never gets commands from the simulation.

- `FighterView` shows the state name and the `StateFrame` above the fighter (until there is art).
- Debug drawing (F2): hurtboxes and active hitboxes of each fighter.
- HUD: one health bar for each active player.
- Animation later (M10): the view reads `(StateId, StateFrame)`, finds the animation by the state `Name`, and seeks the `AnimationPlayer` to that frame each render. It never lets the animation play on its own clock. After a rollback, the next render is correct.
- VFX and sound later (M10): a presentation timeline for each state name ("frame 4: play sound X"). The view fires the events between the last rendered frame and the new one. A filter keyed by (fighter, state start frame, event) prevents most duplicates after a rollback.

### 12.11 Authoring

1. **M4: code-first.** Characters are built in C# with a builder (in the simulation library). The format changes often during the first combat work; code is fast to change and to test.
2. **M10: editor authoring**, the same pattern as the stage: a character scene with one node for each state and hitbox child nodes with frame ranges, next to the sprite and `AnimationPlayer`. An editor tool with a frame slider shows the sprite and the active boxes on each frame. A converter produces the same `FighterDefinitionData`. Hooks are referenced by name, from a fixed read-only registry.

`FighterDefinitionData` gets `ComputeHash()` (like `StageData`), so peers can compare it in M7.

### 12.12 Implementation steps

Each step stops for review.

1. **State machine core.** `FighterDefinitionData`, `FighterStateData`, transitions, conditions, movement modes, hooks, builder. Convert the M2 movement (`Idle`, `Walk`, `JumpSquat`, `Jump`, `DoubleJump`, `Fall`, `Land`) to it. The M2 movement tests must still pass (with the new state names). (Done. All M2 movement tests pass without a change of what they check. 28 new tests for the state machine rules and the definition hash.)
   - Files: `FighterStateData.cs` (state data, transitions, conditions, frame actions, enums), `StateHooks.cs` (hook delegates, `StateContext`), `FighterDefinitionData.cs`, `FighterDefinitionBuilder.cs`, `FighterStateMachine.cs` (phase 1), `FighterMovement.cs` (movement modes, collision, landing / leave ground), `DefaultGameData.CreateFighterDefinition()`.
   - `GameData.Fighter` (`FighterStats`) is replaced by `GameData.FighterDefinition`; the stats are `FighterDefinition.Stats`.
   - `JumpSquatFrames` and `LandFrames` are removed from `FighterStats`: they are the `Duration` of the `JumpSquat` and `Land` states.
   - Drop-through is a state (`PlatformDrop`) with a `StartDropThrough` frame action; its `OnLeaveGround` is `Fall`.
   - `JumpSquat` uses `MovementMode.None`, so the fighter keeps its walk momentum into the jump (as in M2).
   - The jump count is independent of the states: landing sets `JumpsLeft = MaxJumps`; leaving the ground limits it to `MaxJumps - 1` (a jump uses its jump before it leaves the ground).
   - `PrevInput` is stored at the end of the tick (a new last phase), so phase 1 and phase 2 see the same pressed and released buttons.
   - `MovementMode.Knockback` uses the air friction for now (step 2 can add a ground friction).
2. **Combat.** Input `Attack1`/`Attack2`, attack states, hitboxes, hurtboxes, health, hitstun, knockback, hit registry, simultaneous resolution, hitstop. Tests: hit, no double hit, trade, slot order does not change the result, hitstop on and off, armor flag. (Done. 31 new tests. Two deliberate code breaks (knockback `=` in place of `+=`, no hit registry check) were found by the tests.)
   - `FighterCombat.cs`: detection writes only to local arrays; resolution combines several hits on one fighter with order-independent operations (sum of damage, sum of knockback vectors, maximum hitstun and hitstop, OR of `HitTargets`).
   - Tick phases: 0 hitstop (a fighter with `HitstopFrames > 0` counts down and is frozen: no state update, no movement, no input memory), 1 state, 2 movement, 3+4 hits, 5 input memory.
   - `Duration(frames, next, nextInAir)`: a state can end in a different state in the air (`NextStateInAir`). All attacks end in `Idle` on the ground and `Fall` in the air.
   - New condition `HitstunEnded` (`StateFrame >= HitstunFrames`). `Hitstun`: `HitstunEnded` + `Grounded` → `Idle`, `HitstunEnded` → `Fall`.
   - `FighterStats` is a `record` (tests use `with`). New stats: `MaxHealth` (100), `GroundFriction` (1, for `Knockback` on the ground).
   - `MatchRulesData` (`HitstopEnabled`, default true) is `GameData.Rules`.
   - `FighterDefinitionData`: `HitstunState` (optional: without it, hits do damage only) and `DefaultHurtboxes` (the body box 48 x 96).
   - Default attack values: light attacks 18 frames, hitbox frames 4–6, damage 5, hitstun 12, hitstop 3. Heavy attacks 36 frames, hitbox frames 12–16, damage 15, hitstun 24, hitstop 6. First guesses: tune them in the game.
   - The debug label: one line per value, 2 decimals with a fixed width, monospace font, one block for each active player.
3. **Death and restart.** `Dead`, `MatchPhase.RoundOver`, restart with new spawn positions. Tests. (Done. 11 new tests.)
   - New flag `StateFlags.Intangible`: the state has no hurtboxes (the empty list means "use the default hurtboxes", so a flag is needed). `Dead` uses it.
   - `FighterDefinitionData.DeadState` (optional). When a hit brings the health to 0, the fighter changes to `DeadState` (armor does not prevent it), and the body keeps the knockback of the hit (`Dead` uses `MovementMode.Knockback`).
   - `MatchRules.cs` runs at the end of each tick. A fighter with 0 health is defeated. The round ends when at most one fighter is left (2+ players; also a double defeat), or when the single player is defeated.
   - `MatchRulesData.RestartDelayFrames` (default 120).
   - `WorldData.Round` (from 1). `WorldData.StartRound` is used by `Create` and by the restart: full health, idle state, new spawn positions from `Rng`. `Frame` never resets. `PrevInput` is kept, so a button held across the restart is not a new press.
   - The debug label shows the round and the phase.
4. **Presentation.** State name label, hitbox and hurtbox debug drawing, health bars. Test in the running game. (Done.)
   - `FighterView` draws the state name and state frame above the fighter, and (with `ShowCombatDebug` / F2) the active hurtboxes (green outline) and hitboxes (red). With the combat boxes on, the hurtbox outline replaces the body outline. An `Intangible` fighter (defeated) is drawn faded.
   - With F2, a state progress bar above the fighter (48 x 4 px) for states with a fixed length: the state `Duration`, or `HitstunFrames` for the hitstun state. A thin red strip under the bar marks the frames with an active hitbox (startup, active, and recovery are visible before and after the hitbox appears).
   - `HudView` (node `Hud` in `Match.tscn`, at (40, 612) in the floor area): one health bar for each active player in the player color, and "P1 wins" / "Draw" during `RoundOver`.
   - `ShowCombatDebug` is on in `Match.tscn` until there is art.
   - Checked in the running game with `Engine.TimeScale = 0.02` (to catch the hitbox frames in a screenshot).

### 12.13 Not in M4 (the design allows them later)

- Projectiles: a fixed-size projectile array in `WorldData` (no allocation, rollback-safe). They need their own design.
- Charged attacks: `InputHeld`/`InputReleased` conditions and `StateVar0`–`StateVar3` already allow them.
- Blocking or shields, grabs: new states, flags, and hit rules.
- Cancel windows between attacks: transitions with frame windows already allow them.
- Knockback that scales with damage.
- An input buffer (a press stays valid for a few frames), so a press on the last frame of a busy state is not lost.

---

## 13. Detailed Design: M5 Local Multiplayer

Status: **Agreed** (2026-09-30).

### 13.1 Goals

- App flow: main menu → local lobby → match → (Esc) → lobby.
- Up to `GameConstants.MaxPlayers` players on one machine, each with its own input device (keyboard set or controller).
- Input comes from actions in the InputMap (Project Settings), so keys and buttons can change without code.
- A session layer between `MatchRunner` and the simulation, so M6 (rollback) can replace the local session without a change to `MatchRunner`.

### 13.2 Input actions (Project Settings)

All keyboard events use the **physical** keycode (the key position: WASD stays WASD on an AZERTY keyboard).

| Set | Actions | Default bindings |
|---|---|---|
| Keyboard 1 | `keyboard1_left/right/up/down/jump/attack1/attack2` | A D W S, Space, J, K |
| Keyboard 2 | `keyboard2_left/right/up/down/jump/attack1/attack2` | arrows, keypad 0, keypad 1, keypad 2 |
| Controller template | `controller_left/right/up/down/jump/attack1/attack2` | left stick and D-pad, A (jump), X (attack1), B (attack2). Device: "All devices". Dead zone 0.5 |
| Global | `match_pause` | Esc, controller Start |

Controllers use one **template set**. At startup (and when a controller connects), the code copies each template action for each connected controller: `controller1_*` for the first controller, `controller2_*` for the second, and so on, with the device number of that controller. So the controller layout is edited in one place, and the number of controllers is not fixed.

Limits (accepted for now):
- Godot numbers controllers in connection order. A controller that reconnects can get another number.
- Input is read once per tick (16.7 ms). A press and release between two reads is lost. Later fix: record presses from input events until the next tick.

### 13.3 Input devices (`project/src/PlayerInput/`)

| Type | Content |
|---|---|
| `InputDevice` | One input set: `Id` (`keyboard1`, `controller2`, ...), `DisplayName` ("Keyboard 1", "Controller 2"), action prefix. `InputFlags Read()` reads its actions with `Input.IsActionPressed`. `IsJoinPress(InputEvent)` (jump or attack1 pressed), `IsLeavePress(InputEvent)` (attack2 pressed). |
| `InputDevices` | The list of available devices: the two keyboard sets and one device per connected controller. Creates the `controllerN_*` actions from the template. Listens to `Input.JoyConnectionChanged` and raises `DevicesChanged`. |

`InputDevice` replaces `KeyboardInputMap`.

### 13.4 Session layer (`project/simulation/Session/`, pure C#)

```csharp
public interface IMatchSession
{
    GameData Data { get; }
    ref readonly WorldData World { get; }
    void SetLocalInput(int slot, InputFlags input);   // input of a local player for the next frame
    void AdvanceFrame();                               // run one simulation frame
}
```

- `LocalSession`: all players are local. `AdvanceFrame` builds a `FrameInput` from the local inputs and calls `Simulator.Tick`.
- M6 adds `RollbackSession` (local inputs + predicted remote inputs, rollback). `MatchRunner` keeps the same calls.
- Unit tests for `LocalSession`.

### 13.5 App root and `MatchSetup` (`project/src/App/`)

- `Main.tscn` (root `Main` node, `Main.cs`) becomes the **main scene** (`run/main_scene`). It shows one screen at a time as a child: the main menu, the lobby, or the match. It owns `InputDevices`.
- Screens do not know `Main`. They raise events (C# events or signals), and `Main` changes the screen:
  - `MainMenu`: `PlayLocalPressed`, `QuitPressed`.
  - `Lobby`: `StartPressed(MatchSetup)`, `BackPressed`.
  - `MatchRunner`: `ExitRequested` (Esc / Start).
- `MatchSetup` (made by the lobby, read-only after that):
  - player slots: for each slot 0 to N-1, the `InputDevice`;
  - seed (a new random value for each match; chosen outside the simulation, then fixed for the match);
  - stage scene (`PackedScene`) and `MatchRulesData`.
- `Main` gives `MatchSetup` to `MatchRunner` before the match enters the tree (`MatchRunner.Setup(MatchSetup)`). Without a setup (when `Match.tscn` runs alone with F6), `MatchRunner` builds a default setup from its exports, as now.
- Esc (or controller Start) in a match: back to the lobby, with the same players in their slots. Later: a pause menu.

### 13.6 Main menu and lobby

Main menu (`MainMenu.tscn`, script `MainMenuScreen`): title, **Play Local**, **Play Online** (disabled until M7), **Quit**.

Lobby (`Lobby.tscn`, script `LobbyScreen`):
- One `LobbyPlayerSlot` for each player slot, created in code from `GameConstants.MaxPlayers` (no fixed count in the scene).
- A free slot shows "Press a button to join". A joined slot shows "Player N", the device name, and the player color.
- Join: `Jump` or `Attack1` on a device that is not in a slot puts it in the first free slot.
- Leave: `Attack2` on a joined device frees its slot.
- **Start** (enabled with at least 1 player) and **Back** (to the main menu).
- Join and leave presses are read in `_Input` and marked as handled, so a join press (for example Space) never also presses a focused button (`ui_accept` uses Space and Enter).

Player colors move to one shared place (`PlayerColors`), used by the lobby, the fighter views, and the HUD.

### 13.7 Folders

Scenes, one folder for each part of the game (each folder holds all scenes of that part, also UI):

```
project/scenes/
  Main.tscn                     app root (main scene)
  main_menu/MainMenu.tscn
  lobby/Lobby.tscn
  lobby/LobbyPlayerSlot.tscn
  match/Match.tscn
  stages/StageBase.tscn
  stages/Stage01.tscn
```

The user moved the existing scenes in the editor FileSystem dock (which updates all references). Scene folders use snake_case; script folders under `src/` use PascalCase (like the C# namespaces). This can be made consistent later. Screen classes have the suffix `Screen` (`MainMenuScreen`, `LobbyScreen`): a class with the same name as its namespace (`FightingGame.MainMenu.MainMenu`) gives ambiguous names.

Scripts: new code goes into new folders (`src/App/`, `src/PlayerInput/`, `src/MainMenu/`, `src/Lobby/`). The input folder and namespace are `PlayerInput`, not `Input`: a namespace `FightingGame.Input` would hide Godot's `Input` class in all code under `FightingGame.*`. The existing folders (`src/Presentation/`, `src/Authoring/`) stay.

### 13.8 Implementation steps

Each step stops for review.

1. **Input.** Create the input actions (MCP), `InputDevice`, `InputDevices`. `MatchRunner` uses `InputDevice` (`keyboard1`, `keyboard2`) in place of `KeyboardInputMap`. Test in the game, also with a controller if one is available. (Done. 22 actions created with `scripts/godot-rpc.mjs` (plugin command `set_input_action`, physical keys, device "All devices"). Both keyboard sets tested in the game. Controllers not tested: no controller on the development machine. `KeyboardInputMap` is deleted. `MatchRunner` gives slot i the i-th device of `InputDevices.All` until the lobby exists.)
2. **Session.** `IMatchSession`, `LocalSession`, tests. `MatchRunner` uses the session. (Done. 6 new tests: same start as `WorldData.Create`, same frames as `Simulator.Tick` over 300 random frames, inputs cleared after each frame, invalid slot rejected. `LocalSession` clears the inputs after each frame: a slot without new input has no buttons held, never the old buttons. `MatchRunner` has no `WorldData` of its own any more: it calls `SetLocalInput` and `AdvanceFrame`, and the views read `IMatchSession.World`.)
3. **App root and main menu.** The user moves the existing scenes. `Main.tscn`, `MatchSetup`, `MainMenu.tscn`, screen switching, main scene setting. Flow: menu → match (with a default setup) → Esc → menu. (Done. Tested in the game: menu → Enter → match with 2 players → Esc → menu. `Match.tscn` still runs alone.)
   - `Main.tscn` (root `Node` with `Main.cs`) is the main scene. Exports: `MainMenuScene`, `MatchScene`, `StageScene`. Until the lobby exists, "Play Local" starts a match with the first two input devices.
   - `MainMenuScreen` (script of `MainMenu.tscn`): `CenterContainer` > `VBoxContainer` with the title and the three buttons. "Play Local" has the focus at start, so keyboard and controller navigation work (`ui_*` actions).
   - `MatchRunner.Setup(MatchSetup)` must be called before the match enters the tree. Without it, the match builds a default setup from its exports; slots without an input device are not created.
   - `MatchRunner.ExitRequested` is raised on `match_pause` (Esc, controller Start).
   - `PlayerColors` (`src/Presentation/PlayerColors.cs`) holds the slot colors for all views.
4. **Lobby.** `Lobby.tscn`, `LobbyPlayerSlot.tscn`, join and leave, start and back. Full flow: menu → lobby → match → Esc → lobby. (Done. Tested in the game: join with two keyboard sets, leave, join again, start, Esc back to the lobby with the same players.)
   - `LobbyScreen.StartPressed` sends only the list of slot devices (the lobby does not know the stage or the seed); `Main` builds the `MatchSetup`.
   - The Start button has the focus. Join and leave presses are consumed in `_Input`. Any other press reaches the UI: a joined player who presses jump again (Space, controller A = `ui_accept`) presses Start. `ui_cancel` (Esc) = Back.
   - When a player leaves, the players after it move up one slot (no gaps: the slots of a match are 0 to N-1).
   - After a match, the same players keep their slots (`LobbyScreen.Initialize(devices, previousPlayers)`). A controller that disconnects is removed from its slot.
   - `LobbyPlayerSlot.tscn`: `PanelContainer` with a color bar, the title ("Player N" in the player color, or "Press a button to join"), and the device name.
