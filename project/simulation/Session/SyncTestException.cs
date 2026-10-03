using System;
using System.Collections.Generic;
using System.Text;

namespace FightingGame.Session;

/// <summary>
/// A frame that was run again from a snapshot gave a different state than the first run. This is a determinism bug:
/// online, it would be a desync after a rollback.
/// </summary>
public sealed class SyncTestException : Exception
{
    public SyncTestException(int frame, ulong expectedHash, ulong actualHash, IReadOnlyList<FieldDifference> differences)
        : base(CreateMessage(frame, expectedHash, actualHash, differences))
    {
        Frame = frame;
        ExpectedHash = expectedHash;
        ActualHash = actualHash;
        Differences = differences;
    }

    /// <summary>The first frame with a different state.</summary>
    public int Frame { get; }

    /// <summary>The hash of the first run.</summary>
    public ulong ExpectedHash { get; }

    /// <summary>The hash of the run from the snapshot.</summary>
    public ulong ActualHash { get; }

    /// <summary>The fields that differ (first run -> run from the snapshot).</summary>
    public IReadOnlyList<FieldDifference> Differences { get; }

    private static string CreateMessage(int frame, ulong expectedHash, ulong actualHash, IReadOnlyList<FieldDifference> differences)
    {
        var text = new StringBuilder();
        text.Append($"SyncTest: frame {frame} is different when it runs again from a snapshot ");
        text.Append($"(hash {expectedHash:X16} -> {actualHash:X16}).");
        if (differences.Count == 0)
        {
            text.Append(" No field differs: the hash code reads something that is not a field of the state.");
        }
        foreach (FieldDifference difference in differences)
        {
            text.Append('\n').Append("  ").Append(difference);
        }
        return text.ToString();
    }
}
