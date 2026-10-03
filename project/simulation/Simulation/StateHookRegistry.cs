using System;
using System.Collections.Generic;
using System.Linq;

namespace FightingGame.Simulation;

/// <summary>
/// The hooks and custom conditions that fighter data can use, by name. Fighters authored in the editor select them
/// by name, so a new fighter needs no C# code. A new hook or condition is a C# feature: write it as a static method
/// (see the rules of <see cref="StateHook"/>), and add one line to <see cref="Default"/>.
/// </summary>
/// <remarks>
/// A registry never changes after it is created. Do not rename an entry: authored fighters keep the name.
/// </remarks>
public sealed class StateHookRegistry
{
    private readonly Dictionary<string, StateHook> _hooks;
    private readonly Dictionary<string, StateCondition> _conditions;

    /// <summary>The registry of the game. It is empty until the first hook or custom condition is needed.</summary>
    public static StateHookRegistry Default { get; } = new(
        hooks: Array.Empty<KeyValuePair<string, StateHook>>(),
        conditions: Array.Empty<KeyValuePair<string, StateCondition>>());

    /// <summary>Throws <see cref="ArgumentException"/> if a name is empty or used twice in the same list.</summary>
    public StateHookRegistry(IEnumerable<KeyValuePair<string, StateHook>> hooks, IEnumerable<KeyValuePair<string, StateCondition>> conditions)
    {
        _hooks = ToDictionary(hooks, "hook");
        _conditions = ToDictionary(conditions, "condition");
        HookNames = _hooks.Keys.Order(StringComparer.Ordinal).ToArray();
        ConditionNames = _conditions.Keys.Order(StringComparer.Ordinal).ToArray();
    }

    /// <summary>All hook names, in ordinal order (for the inspector dropdowns).</summary>
    public IReadOnlyList<string> HookNames { get; }

    /// <summary>All custom condition names, in ordinal order (for the inspector dropdowns).</summary>
    public IReadOnlyList<string> ConditionNames { get; }

    public bool TryGetHook(string name, out StateHook hook) => _hooks.TryGetValue(name, out hook!);

    public bool TryGetCondition(string name, out StateCondition condition) => _conditions.TryGetValue(name, out condition!);

    private static Dictionary<string, T> ToDictionary<T>(IEnumerable<KeyValuePair<string, T>> entries, string kind) where T : Delegate
    {
        var dictionary = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach ((string name, T value) in entries)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException($"A {kind} name is empty.");
            }
            if (!dictionary.TryAdd(name, value))
            {
                throw new ArgumentException($"The {kind} name '{name}' is used twice.");
            }
        }
        return dictionary;
    }
}
