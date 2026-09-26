using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using FightingGame.Core;
using FightingGame.Simulation;
using Xunit;

namespace FightingGame.Simulation.Tests.Simulation;

public class WorldStateTests
{
    [Fact]
    public void CreateActivatesOnlyTheRequestedSlots()
    {
        GameData data = DefaultGameData.Create();
        WorldState state = WorldState.Create(data, 2, 1);

        Assert.True(state.Fighters[0].Active);
        Assert.True(state.Fighters[1].Active);
        Assert.False(state.Fighters[2].Active);
        Assert.False(state.Fighters[3].Active);
        Assert.Equal(data.Stage.SpawnPoints[0], state.Fighters[0].Position);
        Assert.Equal(data.Stage.SpawnPoints[1], state.Fighters[1].Position);
    }

    [Fact]
    public void CreateFacesTheStageCenter()
    {
        WorldState state = WorldState.Create(DefaultGameData.Create(), 2, 1);
        Assert.Equal(1, state.Fighters[0].Facing);   // Left spawn
        Assert.Equal(-1, state.Fighters[1].Facing);  // Right spawn
    }

    [Theory]
    [InlineData(0)]
    [InlineData(GameConstants.MaxPlayers + 1)]
    public void CreateRejectsInvalidPlayerCount(int playerCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WorldState.Create(DefaultGameData.Create(), playerCount, 1));
    }

    /// <summary>
    /// Changes each primitive field of WorldState (also the fields inside nested structs) one at a time,
    /// and checks that the hash changes. This test fails if a field is missing from a Hash method.
    /// </summary>
    [Fact]
    public void EveryFieldChangesTheHash()
    {
        object baseline = default(WorldState);
        ulong baselineHash = ((WorldState)baseline).ComputeHash();

        List<(string Path, object State)> mutations = Mutations(baseline, typeof(WorldState), "WorldState").ToList();
        Assert.NotEmpty(mutations);

        foreach ((string path, object mutated) in mutations)
        {
            ulong hash = ((WorldState)mutated).ComputeHash();
            Assert.True(hash != baselineHash, $"Field {path} does not change the hash.");
        }
    }

    [Fact]
    public void EveryFighterSlotChangesTheHash()
    {
        // The reflection test sees only the first element of the inline array. This test covers the other slots.
        WorldState baseline = default;
        for (int i = 0; i < GameConstants.MaxPlayers; i++)
        {
            WorldState changed = default;
            changed.Fighters[i].ActionFrame = 1;
            Assert.NotEqual(baseline.ComputeHash(), changed.ComputeHash());
        }
    }

    [Fact]
    public void SnapshotCopyIsIndependent()
    {
        var world = new TestWorld();
        WorldState snapshot = world.State;
        world.Run(10, InputFlags.Right);
        Assert.NotEqual(snapshot.ComputeHash(), world.State.ComputeHash());
        Assert.Equal(0, snapshot.Frame);
        Assert.Equal(world.Data.Stage.SpawnPoints[0], snapshot.Fighters[0].Position);
    }

    [Fact]
    public void RestoreAndReplayGivesTheSameState()
    {
        InputFlags[] inputs = RandomInputs(seed: 42, count: 600);

        var world = new TestWorld(playerCount: 1);
        world.Run(100, InputFlags.Right);
        WorldState snapshot = world.State;

        foreach (InputFlags input in inputs)
        {
            world.Tick(input);
        }
        ulong firstHash = world.State.ComputeHash();

        world.State = snapshot;
        foreach (InputFlags input in inputs)
        {
            world.Tick(input);
        }
        Assert.Equal(firstHash, world.State.ComputeHash());
    }

    [Fact]
    public void TwoRunsWithTheSameInputGiveTheSameHashOnEveryFrame()
    {
        InputFlags[] p0 = RandomInputs(seed: 1, count: 1000);
        InputFlags[] p1 = RandomInputs(seed: 2, count: 1000);

        var a = new TestWorld(playerCount: 2);
        var b = new TestWorld(playerCount: 2);
        for (int i = 0; i < p0.Length; i++)
        {
            a.Tick(p0[i], p1[i]);
            b.Tick(p0[i], p1[i]);
            Assert.Equal(a.State.ComputeHash(), b.State.ComputeHash());
        }
    }

    /// <summary>Random but repeatable input. Each input is held for a few frames, like a real player.</summary>
    internal static InputFlags[] RandomInputs(ulong seed, int count)
    {
        var rng = new FixedRng(seed);
        var inputs = new InputFlags[count];
        InputFlags current = InputFlags.None;
        for (int i = 0; i < count; i++)
        {
            if (rng.NextInt(6) == 0)
            {
                current = (InputFlags)rng.NextInt(1 << 6);
            }
            inputs[i] = current;
        }
        return inputs;
    }

    private static IEnumerable<(string Path, object State)> Mutations(object boxed, Type type, string path)
    {
        foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            Type fieldType = field.FieldType;
            string fieldPath = $"{path}.{field.Name}";

            if (fieldType.IsPrimitive || fieldType.IsEnum)
            {
                object copy = RuntimeHelpers.GetObjectValue(boxed)!;
                field.SetValue(copy, OneOf(fieldType));
                yield return (fieldPath, copy);
            }
            else if (fieldType.IsValueType)
            {
                object inner = field.GetValue(boxed)!;
                foreach ((string innerPath, object innerMutated) in Mutations(inner, fieldType, fieldPath))
                {
                    object copy = RuntimeHelpers.GetObjectValue(boxed)!;
                    field.SetValue(copy, innerMutated);
                    yield return (innerPath, copy);
                }
            }
            else
            {
                throw new InvalidOperationException($"{fieldPath} is a reference type. The world state must contain only value types.");
            }
        }
    }

    private static object OneOf(Type type) =>
        type == typeof(bool) ? true :
        type.IsEnum ? Enum.ToObject(type, 1) :
        Convert.ChangeType(1, type);
}
