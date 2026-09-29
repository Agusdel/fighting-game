using System.Linq;
using FightingGame.Core;
using FightingGame.Simulation;
using Xunit;

namespace FightingGame.Simulation.Tests.Simulation;

public class MatchRulesTests
{
    private const InputFlags Light = InputFlags.Attack1;

    // The light attack hits on the 5th tick after the press (hitbox on state frames 4-6). Damage 5.
    private const int LightHitTick = 5;

    private static readonly MatchRulesData Rules = new() { HitstopEnabled = false, RestartDelayFrames = 30 };

    /// <summary>Slot 0 at x = 400 facing right, slot 1 at x = 450 facing left, with 5 health (one light hit is a defeat).</summary>
    private static TestWorld Duel(int playerCount = 2)
    {
        var world = new TestWorld(playerCount: playerCount, rules: Rules);
        world.PlaceOnFloor(0, 400, 1);
        world.PlaceOnFloor(1, 450, -1);
        world.Fighter(1).Health = 5;
        return world;
    }

    private static void LightAttackToTheHit(TestWorld world, InputFlags p1 = InputFlags.None)
    {
        world.Tick(Light, p1);
        world.Run(LightHitTick - 1);
    }

    [Fact]
    public void ZeroHealthIsADefeat()
    {
        TestWorld world = Duel();
        LightAttackToTheHit(world);

        Assert.Equal(0, world.Fighter(1).Health);
        Assert.Equal("Dead", world.StateName(1));
        Assert.Equal(new FixedVector2(4, -2), world.Fighter(1).Velocity);   // The body keeps the knockback.
    }

    [Fact]
    public void ADefeatedFighterCannotBeHit()
    {
        TestWorld world = Duel();
        LightAttackToTheHit(world);
        world.Run(20);

        world.PlaceOnFloor(1, 450, -1);
        world.Tick(Light);
        world.Run(LightHitTick);
        Assert.Equal(0, world.Fighter(0).HitTargets);
        Assert.Equal("Dead", world.StateName(1));
    }

    [Fact]
    public void RoundEndsWhenOneFighterIsLeft()
    {
        TestWorld world = Duel();
        Assert.Equal(1, world.State.Round);
        world.Tick(Light);
        world.Run(LightHitTick - 2);
        Assert.Equal(MatchPhase.Fighting, world.State.Phase);

        world.Tick();
        Assert.Equal(MatchPhase.RoundOver, world.State.Phase);
        Assert.Equal(0, world.State.PhaseTimer);
    }

    [Fact]
    public void RoundContinuesWhileTwoFightersAreLeft()
    {
        TestWorld world = Duel(playerCount: 3);
        LightAttackToTheHit(world);
        Assert.Equal("Dead", world.StateName(1));
        Assert.Equal(MatchPhase.Fighting, world.State.Phase);
    }

    [Fact]
    public void ADoubleDefeatEndsTheRound()
    {
        TestWorld world = Duel();
        world.Fighter(0).Health = 5;
        LightAttackToTheHit(world, p1: Light);

        Assert.Equal("Dead", world.StateName(0));
        Assert.Equal("Dead", world.StateName(1));
        Assert.Equal(MatchPhase.RoundOver, world.State.Phase);
    }

    [Fact]
    public void TheWinnerCanMoveDuringRoundOver()
    {
        TestWorld world = Duel();
        LightAttackToTheHit(world);
        world.Run(20);
        Fixed x = world.Fighter(0).Position.X;

        world.Run(5, InputFlags.Left);
        Assert.Equal(MatchPhase.RoundOver, world.State.Phase);
        Assert.True(world.Fighter(0).Position.X < x);
    }

    [Fact]
    public void NextRoundStartsAfterTheDelay()
    {
        TestWorld world = Duel();
        LightAttackToTheHit(world);
        int frameAtRoundOver = world.State.Frame;
        ulong rngAtRoundOver = world.State.Rng.State;

        world.Run(Rules.RestartDelayFrames - 1);
        Assert.Equal(MatchPhase.RoundOver, world.State.Phase);

        world.Tick();
        Assert.Equal(MatchPhase.Fighting, world.State.Phase);
        Assert.Equal(2, world.State.Round);
        Assert.Equal(frameAtRoundOver + Rules.RestartDelayFrames, world.State.Frame);   // Frame never resets.
        Assert.NotEqual(rngAtRoundOver, world.State.Rng.State);                         // New spawn positions were chosen.

        FighterStats stats = world.Stats;
        FixedVector2[] allSpawnPositions = world.Data.Stage.SpawnPositionPairs
            .SelectMany(p => new[] { p.A, p.B })
            .Concat(world.Data.Stage.SingleSpawnPositions)
            .ToArray();
        for (int i = 0; i < 2; i++)
        {
            FighterData fighter = world.Fighter(i);
            Assert.Equal(stats.MaxHealth, fighter.Health);
            Assert.Equal("Idle", world.StateName(i));
            Assert.Equal(0, fighter.StateFrame);
            Assert.Equal(FixedVector2.Zero, fighter.Velocity);
            Assert.Contains(fighter.Position, allSpawnPositions);
        }
    }

    [Fact]
    public void AButtonHeldAcrossTheRestartIsNotAPress()
    {
        TestWorld world = Duel();
        LightAttackToTheHit(world);
        world.Run(Rules.RestartDelayFrames, Light);
        Assert.Equal(2, world.State.Round);

        world.Tick(Light);
        Assert.Equal("Idle", world.StateName(0));
    }

    [Fact]
    public void OnePlayerRoundDoesNotEndWhileTheFighterStands()
    {
        var world = new TestWorld(playerCount: 1, rules: Rules);
        world.Run(200);
        Assert.Equal(MatchPhase.Fighting, world.State.Phase);
        Assert.Equal(1, world.State.Round);
    }

    [Fact]
    public void ArmorDoesNotPreventADefeat()
    {
        static FixedAABB Box(int minX, int minY, int maxX, int maxY) =>
            new(new FixedVector2(minX, minY), new FixedVector2(maxX, maxY));

        var builder = new FighterDefinitionBuilder(DefaultGameData.CreateFighterStats())
        {
            IdleState = "Idle",
            HitstunState = "Hitstun",
            DeadState = "Dead",
        };
        builder.DefaultHurtbox(Box(-24, -96, 24, 0));
        builder.State("Idle", s => s
            .Flags(StateFlags.ArmoredAgainstHits)
            .Transition("Punch", ConditionData.Pressed(Light)));
        builder.State("Punch", s => s
            .Duration(10, "Idle")
            .Hitbox(Box(20, -70, 64, -40), 1, 3, damage: 7, hitstunFrames: 10, knockback: FixedVector2.Zero, hitstopFrames: 0));
        builder.State("Hitstun", s => s.Transition("Idle", ConditionData.HitstunEnded));
        builder.State("Dead", s => s.Flags(StateFlags.Intangible));

        var world = new TestWorld(playerCount: 2, definition: builder.Build(), rules: Rules);
        world.PlaceOnFloor(0, 400, 1);
        world.PlaceOnFloor(1, 450, -1);
        world.Fighter(1).Health = 5;
        world.Tick(Light);
        world.Tick();

        Assert.Equal("Dead", world.StateName(1));
    }

    [Fact]
    public void MatchesWithManyRoundsAreDeterministic()
    {
        InputFlags[] p0 = WorldDataTests.RandomInputs(seed: 71, count: 3000);
        InputFlags[] p1 = WorldDataTests.RandomInputs(seed: 72, count: 3000);
        // The first input is a light attack on a target with 5 health, so round 1 always ends.
        // The random input then continues through the restart.
        p0[0] = Light;
        p1[0] = InputFlags.None;

        TestWorld CreateWorld()
        {
            var world = new TestWorld(playerCount: 2, rules: new MatchRulesData { RestartDelayFrames = 30 });
            world.PlaceOnFloor(0, 500, 1);
            world.PlaceOnFloor(1, 560, -1);
            world.Fighter(1).Health = 5;
            return world;
        }

        TestWorld a = CreateWorld();
        TestWorld b = CreateWorld();
        for (int i = 0; i < p0.Length; i++)
        {
            a.Tick(p0[i], p1[i]);
            b.Tick(p0[i], p1[i]);
            Assert.Equal(a.State.ComputeHash(), b.State.ComputeHash());
        }

        Assert.True(a.State.Round > 1, "The random inputs must end at least one round, or the test proves nothing.");
    }
}
