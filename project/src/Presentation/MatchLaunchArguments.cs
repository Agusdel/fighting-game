using System;
using System.Collections.Generic;
using System.Globalization;
using FightingGame.App;
using Godot;

namespace FightingGame.Presentation;

/// <summary>
/// Optional command-line values for a match scene that runs alone (not from the main menu). They replace the exports
/// of <see cref="MatchRunner"/>. Give them after <c>--</c>, for example:
/// <code>godot --path project res://scenes/match/Match.tscn -- --players=3 --session=loopback --latency=80</code>
/// </summary>
/// <remarks>
/// Values: <c>--players=N</c> (1 to 4), <c>--seed=N</c>, <c>--session=local|synctest|loopback</c>,
/// <c>--latency=MS</c>, <c>--jitter=MS</c>, <c>--loss=PERCENT</c> (loopback network).
/// An unknown or invalid value is reported as a warning and not used.
/// </remarks>
public sealed class MatchLaunchArguments
{
    public int? Players { get; private set; }
    public ulong? Seed { get; private set; }
    public MatchSessionType? SessionType { get; private set; }
    public int? LatencyMs { get; private set; }
    public int? JitterMs { get; private set; }
    public int? LossPercent { get; private set; }

    /// <summary>The problems found while reading the arguments (for warnings).</summary>
    public List<string> Problems { get; } = new();

    /// <summary>Reads the user arguments of this run (the arguments after <c>--</c>).</summary>
    public static MatchLaunchArguments FromCommandLine() => Parse(OS.GetCmdlineUserArgs());

    public static MatchLaunchArguments Parse(IEnumerable<string> arguments)
    {
        var result = new MatchLaunchArguments();
        foreach (string argument in arguments)
        {
            string[] parts = argument.TrimStart('-').Split('=', 2);
            string key = parts[0].ToLowerInvariant();
            string value = parts.Length > 1 ? parts[1] : "";
            if (!result.TryApply(key, value))
            {
                result.Problems.Add($"Unknown or invalid match argument '{argument}'.");
            }
        }
        return result;
    }

    private bool TryApply(string key, string value)
    {
        switch (key)
        {
            case "players" when TryInt(value, 1, 4, out int players):
                Players = players;
                return true;
            case "seed" when ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out ulong seed):
                Seed = seed;
                return true;
            case "session" when Enum.TryParse(value, ignoreCase: true, out MatchSessionType session) && Enum.IsDefined(session):
                SessionType = session;
                return true;
            case "latency" when TryInt(value, 0, 1000, out int latency):
                LatencyMs = latency;
                return true;
            case "jitter" when TryInt(value, 0, 1000, out int jitter):
                JitterMs = jitter;
                return true;
            case "loss" when TryInt(value, 0, 100, out int loss):
                LossPercent = loss;
                return true;
            default:
                return false;
        }
    }

    private static bool TryInt(string text, int min, int max, out int value) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= min && value <= max;
}
