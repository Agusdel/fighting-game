using System;
using FightingGame.Core;

namespace FightingGame.Simulation;

/// <summary>
/// Picks the spawn positions for a match. The result depends only on the stage, the player count, and the random
/// generator state, so every machine gets the same spawn positions.
/// </summary>
/// <remarks>
/// Rules:
/// <list type="number">
/// <item>Pick (player count / 2) different spawn position pairs at random. Each pair gives two opposite positions.</item>
/// <item>If the player count is odd, also pick one of the single spawn positions at random.</item>
/// <item>Shuffle the chosen positions, so the player slot does not decide the position.</item>
/// </list>
/// </remarks>
public static class SpawnPositionSelector
{
    /// <summary>
    /// Writes one spawn position for each player slot 0 to playerCount - 1 into <paramref name="result"/>.
    /// Advances <paramref name="rng"/>.
    /// </summary>
    public static void Select(StageData stage, int playerCount, ref FixedRng rng, Span<FixedVector2> result)
    {
        if (playerCount < 1 || playerCount > GameConstants.MaxPlayers)
        {
            throw new ArgumentOutOfRangeException(nameof(playerCount));
        }
        if (result.Length < playerCount)
        {
            throw new ArgumentException("The result span is too small.", nameof(result));
        }

        int pairCount = stage.SpawnPositionPairs.Length;
        int pairsNeeded = playerCount / 2;

        // Partial Fisher-Yates shuffle: the first pairsNeeded entries become a random choice of different pairs.
        Span<int> pairIndices = pairCount <= 64 ? stackalloc int[pairCount] : new int[pairCount];
        for (int i = 0; i < pairCount; i++)
        {
            pairIndices[i] = i;
        }
        for (int i = 0; i < pairsNeeded; i++)
        {
            int j = rng.NextInt(i, pairCount);
            (pairIndices[i], pairIndices[j]) = (pairIndices[j], pairIndices[i]);
        }

        int count = 0;
        for (int i = 0; i < pairsNeeded; i++)
        {
            SpawnPositionPair pair = stage.SpawnPositionPairs[pairIndices[i]];
            result[count++] = pair.A;
            result[count++] = pair.B;
        }
        if (playerCount % 2 == 1)
        {
            FixedVector2[] singles = stage.SingleSpawnPositions;
            result[count++] = singles[rng.NextInt(singles.Length)];
        }

        // Full Fisher-Yates shuffle of the chosen positions.
        for (int i = count - 1; i > 0; i--)
        {
            int j = rng.NextInt(i + 1);
            (result[i], result[j]) = (result[j], result[i]);
        }
    }
}
