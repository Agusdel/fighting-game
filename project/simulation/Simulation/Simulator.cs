using FightingGame.Core;

namespace FightingGame.Simulation;

/// <summary>
/// Advances the world by one frame. The result depends only on the state, the input, and the static data,
/// so every machine that runs the same frames gets the same state.
/// </summary>
/// <remarks>
/// A tick runs in phases. Each phase runs for all fighters before the next phase starts,
/// and a phase never reads data that another fighter writes in the same phase.
/// So the player slot order cannot change the result.
/// <list type="number">
/// <item>State: the state machine of each fighter (<see cref="FighterStateMachine.Update"/>), which also sets the velocity.</item>
/// <item>Movement: move each fighter and resolve collisions with the stage only. Fighters do not block each other.</item>
/// <item>Input memory: each fighter stores this frame's input, to detect pressed and released buttons on the next frame.</item>
/// </list>
/// </remarks>
public static class Simulator
{
    public static void Tick(ref WorldData state, in FrameInput input, GameData data)
    {
        for (int i = 0; i < GameConstants.MaxPlayers; i++)
        {
            ref FighterData fighter = ref state.Fighters[i];
            if (fighter.Active)
            {
                FighterStateMachine.Update(ref fighter, CreateContext(fighter, input[i], data));
            }
        }

        for (int i = 0; i < GameConstants.MaxPlayers; i++)
        {
            ref FighterData fighter = ref state.Fighters[i];
            if (fighter.Active)
            {
                FighterMovement.MoveAndCollide(ref fighter, CreateContext(fighter, input[i], data));
            }
        }

        for (int i = 0; i < GameConstants.MaxPlayers; i++)
        {
            ref FighterData fighter = ref state.Fighters[i];
            if (fighter.Active)
            {
                fighter.PrevInput = input[i];
            }
        }

        state.Frame++;
        state.PhaseTimer++;
    }

    /// <summary>The context of one fighter on this frame. <see cref="FighterData.PrevInput"/> still holds the last frame's input.</summary>
    public static StateContext CreateContext(in FighterData fighter, InputFlags input, GameData data) =>
        new(input, fighter.PrevInput, data.FighterDefinition, data.Stage);
}
