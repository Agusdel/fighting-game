using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using FightingGame.Core;
using FightingGame.Simulation;
using Xunit;

namespace FightingGame.Simulation.Tests.Simulation;

public class WorldDataTests
{
    [Fact]
    public void CreateActivatesOnlyTheRequestedSlots()
    {
        GameData data = TestStages.CreateGameData();
        WorldData state = WorldData.Create(data, 2, 1);

        Assert.True(state.Fighters[0].Active);
        Assert.True(state.Fighters[1].Active);
        Assert.False(state.Fighters[2].Active);
        Assert.False(state.Fighters[3].Active);
    }

    [Fact]
    public void CreateFacesTheStageCenter()
    {
        GameData data = TestStages.CreateGameData();
        Fixed centerX = (data.Stage.Bounds.Min.X + data.Stage.Bounds.Max.X) * Fixed.Half;
        for (ulong seed = 0; seed < 20; seed++)
        {
            WorldData state = WorldData.Create(data, GameConstants.MaxPlayers, seed);
            for (int i = 0; i < GameConstants.MaxPlayers; i++)
            {
                FighterData fighter = state.Fighters[i];
                Assert.Equal(fighter.Position.X <= centerX ? 1 : -1, fighter.Facing);
            }
        }
    }

    [Fact]
    public void CreateUsesTheSpawnPositionSelection()
    {
        GameData data = TestStages.CreateGameData();
        WorldData state = WorldData.Create(data, 3, 7);

        var rng = new FixedRng(7);
        var expected = new FixedVector2[GameConstants.MaxPlayers];
        SpawnPositionSelector.Select(data.Stage, 3, ref rng, expected);

        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(expected[i], state.Fighters[i].Position);
        }
        Assert.Equal(rng.State, state.Rng.State);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(GameConstants.MaxPlayers + 1)]
    public void CreateRejectsInvalidPlayerCount(int playerCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WorldData.Create(TestStages.CreateGameData(), playerCount, 1));
    }

    /// <summary>
    /// Changes each primitive field of WorldData (also the fields inside nested structs) one at a time,
    /// and checks that the hash changes. This test fails if a field is missing from a Hash method.
    /// </summary>
    [Fact]
    public void EveryFieldChangesTheHash()
    {
        object baseline = default(WorldData);
        ulong baselineHash = ((WorldData)baseline).ComputeHash();

        List<(string Path, object State)> mutations = Mutations(baseline, typeof(WorldData), "WorldData").ToList();
        Assert.NotEmpty(mutations);

        foreach ((string path, object mutated) in mutations)
        {
            ulong hash = ((WorldData)mutated).ComputeHash();
            Assert.True(hash != baselineHash, $"Field {path} does not change the hash.");
        }
    }

    [Fact]
    public void EveryFighterSlotChangesTheHash()
    {
        // The reflection test sees only the first element of the inline array. This test covers the other slots.
        WorldData baseline = default;
        for (int i = 0; i < GameConstants.MaxPlayers; i++)
        {
            WorldData changed = default;
            changed.Fighters[i].StateFrame = 1;
            Assert.NotEqual(baseline.ComputeHash(), changed.ComputeHash());
        }
    }

    [Fact]
    public void SnapshotCopyIsIndependent()
    {
        var world = new TestWorld();
        WorldData snapshot = world.State;
        world.Run(10, InputFlags.Right);
        Assert.NotEqual(snapshot.ComputeHash(), world.State.ComputeHash());
        Assert.Equal(0, snapshot.Frame);
        Assert.Equal(TestWorld.StartPositions[0], snapshot.Fighters[0].Position);
    }

    [Fact]
    public void RestoreAndReplayGivesTheSameState()
    {
        InputFlags[] inputs = RandomInputs(seed: 42, count: 600);

        var world = new TestWorld(playerCount: 1);
        world.Run(100, InputFlags.Right);
        WorldData snapshot = world.State;

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
                current = (InputFlags)rng.NextInt(1 << 7);
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
