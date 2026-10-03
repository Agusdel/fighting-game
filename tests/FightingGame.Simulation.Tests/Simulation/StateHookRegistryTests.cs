using System;
using System.Collections.Generic;
using FightingGame.Simulation;
using Xunit;

namespace FightingGame.Simulation.Tests.Simulation;

public class StateHookRegistryTests
{
    private static void SetVar0(ref FighterData fighter, in StateContext context) => fighter.StateVar0 = 1;
    private static void SetVar1(ref FighterData fighter, in StateContext context) => fighter.StateVar1 = 1;
    private static bool IsFacingLeft(in FighterData fighter, in StateContext context) => fighter.Facing < 0;

    private static KeyValuePair<string, StateHook> Hook(string name, StateHook hook) => new(name, hook);
    private static KeyValuePair<string, StateCondition> Condition(string name, StateCondition condition) => new(name, condition);

    [Fact]
    public void FindsEntriesByName()
    {
        var registry = new StateHookRegistry(
            new[] { Hook("SetVar0", SetVar0), Hook("SetVar1", SetVar1) },
            new[] { Condition("IsFacingLeft", IsFacingLeft) });

        Assert.True(registry.TryGetHook("SetVar1", out StateHook hook));
        Assert.Equal(nameof(SetVar1), hook.Method.Name);
        Assert.True(registry.TryGetCondition("IsFacingLeft", out StateCondition condition));
        Assert.Equal(nameof(IsFacingLeft), condition.Method.Name);

        Assert.False(registry.TryGetHook("setvar1", out _));        // Names are case sensitive.
        Assert.False(registry.TryGetHook("IsFacingLeft", out _));   // Hooks and conditions are separate lists.
        Assert.False(registry.TryGetCondition("Unknown", out _));
    }

    [Fact]
    public void NamesAreInOrdinalOrder()
    {
        var registry = new StateHookRegistry(
            new[] { Hook("b", SetVar0), Hook("B", SetVar1), Hook("a", SetVar0) },
            Array.Empty<KeyValuePair<string, StateCondition>>());

        Assert.Equal(new[] { "B", "a", "b" }, registry.HookNames);
        Assert.Empty(registry.ConditionNames);
    }

    [Fact]
    public void RejectsEmptyAndDuplicateNames()
    {
        var noConditions = Array.Empty<KeyValuePair<string, StateCondition>>();
        Assert.Throws<ArgumentException>(() => new StateHookRegistry(new[] { Hook(" ", SetVar0) }, noConditions));
        Assert.Throws<ArgumentException>(() => new StateHookRegistry(new[] { Hook("A", SetVar0), Hook("A", SetVar1) }, noConditions));
    }

    [Fact]
    public void DefaultRegistryIsValid()
    {
        StateHookRegistry registry = StateHookRegistry.Default;
        Assert.Equal(registry.HookNames.Count, new HashSet<string>(registry.HookNames).Count);
        Assert.Equal(registry.ConditionNames.Count, new HashSet<string>(registry.ConditionNames).Count);
    }
}
