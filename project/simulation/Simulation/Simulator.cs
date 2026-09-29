using System;
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
/// <item>Hitstop: a fighter with hitstop frames left is frozen on this frame. It skips phases 2, 3, and 5.</item>
/// <item>State: the state machine of each fighter (<see cref="FighterStateMachine.Update"/>), which also sets the velocity.</item>
/// <item>Movement: move each fighter and resolve collisions with the stage only. Fighters do not block each other.</item>
/// <item>Hits: detect all hits, then apply them all at the same time (<see cref="FighterCombat"/>).</item>
/// <item>Input memory: each fighter stores this frame's input, to detect pressed and released buttons on the next
/// frame. A frozen fighter keeps its old input, so a button pressed during hitstop counts as pressed after it.</item>
/// <item>Match rules: end the round when at most one fighter is left, and start the next round after a delay
/// (<see cref="MatchRules"/>).</item>
/// </list>
/// </remarks>
public static class Simulator
{
    public static void Tick(ref WorldData state, in FrameInput input, GameData data)
    {
        Span<bool> frozen = stackalloc bool[GameConstants.MaxPlayers];
        for (int i = 0; i < GameConstants.MaxPlayers; i++)
        {
            ref FighterData fighter = ref state.Fighters[i];
            if (fighter.Active && fighter.HitstopFrames > 0)
            {
                fighter.HitstopFrames--;
                frozen[i] = true;
            }
        }

        for (int i = 0; i < GameConstants.MaxPlayers; i++)
        {
            ref FighterData fighter = ref state.Fighters[i];
            if (fighter.Active && !frozen[i])
            {
                FighterStateMachine.Update(ref fighter, CreateContext(fighter, input[i], data));
            }
        }

        for (int i = 0; i < GameConstants.MaxPlayers; i++)
        {
            ref FighterData fighter = ref state.Fighters[i];
            if (fighter.Active && !frozen[i])
            {
                FighterMovement.MoveAndCollide(ref fighter, CreateContext(fighter, input[i], data));
            }
        }

        FighterCombat.DetectAndResolveHits(ref state, input, data);

        for (int i = 0; i < GameConstants.MaxPlayers; i++)
        {
            ref FighterData fighter = ref state.Fighters[i];
            if (fighter.Active && !frozen[i])
            {
                fighter.PrevInput = input[i];
            }
        }

        state.Frame++;
        state.PhaseTimer++;
        MatchRules.Update(ref state, data);
    }

    /// <summary>The context of one fighter on this frame. <see cref="FighterData.PrevInput"/> still holds the last frame's input.</summary>
    public static StateContext CreateContext(in FighterData fighter, InputFlags input, GameData data) =>
        new(input, fighter.PrevInput, data.FighterDefinition, data.Stage);
}
