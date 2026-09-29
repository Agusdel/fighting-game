using FightingGame.Core;

namespace FightingGame.Simulation;

/// <summary>
/// Round rules: when at most one fighter is left, the round is over. After a delay, the next round starts.
/// </summary>
/// <remarks>
/// A fighter is defeated when its health is 0. The round ends:
/// <list type="bullet">
/// <item>with 2 or more players: when at most one fighter is not defeated (also 0, after a double defeat);</item>
/// <item>with 1 player: when that fighter is defeated.</item>
/// </list>
/// During <see cref="MatchPhase.RoundOver"/>, the fighters still run, so the winner can move.
/// </remarks>
public static class MatchRules
{
    /// <summary>Runs at the end of a tick, after <see cref="WorldData.PhaseTimer"/> advanced.</summary>
    public static void Update(ref WorldData state, GameData data)
    {
        switch (state.Phase)
        {
            case MatchPhase.Fighting:
                if (IsRoundOver(state))
                {
                    state.Phase = MatchPhase.RoundOver;
                    state.PhaseTimer = 0;
                }
                break;

            case MatchPhase.RoundOver:
                if (state.PhaseTimer >= data.Rules.RestartDelayFrames)
                {
                    WorldData.StartRound(ref state, data);
                }
                break;
        }
    }

    private static bool IsRoundOver(in WorldData state)
    {
        int active = 0;
        int standing = 0;
        for (int i = 0; i < GameConstants.MaxPlayers; i++)
        {
            FighterData fighter = state.Fighters[i];
            if (fighter.Active)
            {
                active++;
                if (fighter.Health > 0)
                {
                    standing++;
                }
            }
        }

        return active >= 2 ? standing <= 1 : standing == 0;
    }
}
