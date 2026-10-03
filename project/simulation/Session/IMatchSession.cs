using FightingGame.Simulation;

namespace FightingGame.Session;

/// <summary>
/// Runs a match frame by frame. The game loop gives it the input of the local players and asks it to advance.
/// The session decides how the frames run: all players local (<see cref="LocalSession"/>), or with remote
/// players and rollback (<see cref="RollbackSession"/>). The game loop does not change when the session type changes.
/// </summary>
public interface IMatchSession
{
    /// <summary>The static data of the match.</summary>
    GameData Data { get; }

    /// <summary>The current world state, for the views. Read only: only the session changes it.</summary>
    ref readonly WorldData World { get; }

    /// <summary>Sets the input of a local player for the next frame. A slot without input for a frame has no buttons held.</summary>
    void SetLocalInput(int slot, InputFlags input);

    /// <summary>
    /// Runs one simulation frame. Returns false if the session must wait for remote players (no frame ran).
    /// The game loop calls it again on the next tick: the local inputs of a frame that did not run are dropped.
    /// </summary>
    bool AdvanceFrame();
}
