using FightingGame.Core;
using FightingGame.Simulation;
using Xunit;

namespace FightingGame.Simulation.Tests.Simulation;

public class CombatTests
{
    private const InputFlags Light = InputFlags.Attack1;
    private const InputFlags Heavy = InputFlags.Attack2;

    // Default fighter: the light attack hitbox is active on state frames 4-6, the heavy one on frames 12-16.
    // A press starts the attack on state frame 0, so the light hitbox is first active on the 5th tick.
    private const int LightFirstActiveTick = 5;

    private static readonly MatchRulesData NoHitstop = new() { HitstopEnabled = false };

    /// <summary>Two fighters on the floor: slot 0 at x = 400 facing right, slot 1 at 400 + gap facing left.</summary>
    private static TestWorld Duel(int gap = 50, MatchRulesData? rules = null)
    {
        var world = new TestWorld(playerCount: 2, rules: rules ?? NoHitstop);
        world.PlaceOnFloor(0, 400, 1);
        world.PlaceOnFloor(1, 400 + gap, -1);
        return world;
    }

    private static void StartLightAttackAndRunToFirstHit(TestWorld world, InputFlags p1 = InputFlags.None)
    {
        world.Tick(Light, p1);
        world.Run(LightFirstActiveTick - 1);
    }

    [Fact]
    public void LightAttackHitsATargetInFront()
    {
        TestWorld world = Duel();
        StartLightAttackAndRunToFirstHit(world);

        FighterData target = world.Fighter(1);
        Assert.Equal(95, target.Health);
        Assert.Equal("Hitstun", world.StateName(1));
        Assert.Equal(12, target.HitstunFrames);
        Assert.Equal(new FixedVector2(4, -2), target.Velocity);
        Assert.Equal(0b10, world.Fighter(0).HitTargets);
        Assert.Equal(100, world.Fighter(0).Health);
    }

    [Fact]
    public void NoHitBeforeTheActiveFrames()
    {
        TestWorld world = Duel();
        world.Tick(Light);
        world.Run(LightFirstActiveTick - 2);
        Assert.Equal(100, world.Fighter(1).Health);
    }

    [Fact]
    public void AttackDoesNotHitATargetBehind()
    {
        TestWorld world = Duel();
        world.PlaceOnFloor(1, 350, 1);
        world.Tick(Light);
        world.Run(20);
        Assert.Equal(100, world.Fighter(1).Health);
    }

    [Fact]
    public void FacingLeftMirrorsTheHitboxAndTheKnockback()
    {
        TestWorld world = Duel();
        world.PlaceOnFloor(0, 400, -1);
        world.PlaceOnFloor(1, 350, 1);
        StartLightAttackAndRunToFirstHit(world);

        Assert.Equal(95, world.Fighter(1).Health);
        Assert.Equal(new FixedVector2(-4, -2), world.Fighter(1).Velocity);
    }

    [Fact]
    public void AnAttackHitsEachTargetOnlyOnce()
    {
        TestWorld world = Duel();
        world.Tick(Light);
        world.Run(30);
        Assert.Equal(95, world.Fighter(1).Health);
    }

    [Fact]
    public void TwoFightersCanHitEachOtherOnTheSameFrame()
    {
        TestWorld world = Duel();
        StartLightAttackAndRunToFirstHit(world, p1: Light);

        Assert.Equal(95, world.Fighter(0).Health);
        Assert.Equal(95, world.Fighter(1).Health);
        Assert.Equal("Hitstun", world.StateName(0));
        Assert.Equal("Hitstun", world.StateName(1));
    }

    [Fact]
    public void HitsOnTheSameTargetAddUp()
    {
        var world = new TestWorld(playerCount: 3, rules: NoHitstop);
        world.PlaceOnFloor(0, 400, 1);
        world.PlaceOnFloor(1, 450, 1);
        world.PlaceOnFloor(2, 500, -1);

        world.TickAll(Light, InputFlags.None, Light);
        for (int i = 1; i < LightFirstActiveTick; i++)
        {
            world.TickAll();
        }

        FighterData target = world.Fighter(1);
        Assert.Equal(90, target.Health);
        Assert.Equal(new FixedVector2(0, -4), target.Velocity);   // (4, -2) + (-4, -2)
    }

    [Fact]
    public void HitstopFreezesTheAttackerAndTheTarget()
    {
        TestWorld world = Duel(rules: new MatchRulesData { HitstopEnabled = true });
        StartLightAttackAndRunToFirstHit(world);

        Assert.Equal(3, world.Fighter(0).HitstopFrames);
        Assert.Equal(3, world.Fighter(1).HitstopFrames);
        FixedVector2 targetPosition = world.Fighter(1).Position;
        int attackerFrame = world.Fighter(0).StateFrame;

        world.Run(3);
        Assert.Equal(targetPosition, world.Fighter(1).Position);
        Assert.Equal(attackerFrame, world.Fighter(0).StateFrame);
        Assert.Equal(0, world.Fighter(0).HitstopFrames);

        world.Tick();
        Assert.NotEqual(targetPosition, world.Fighter(1).Position);
        Assert.Equal(attackerFrame + 1, world.Fighter(0).StateFrame);
    }

    [Fact]
    public void WithoutHitstopNothingFreezes()
    {
        TestWorld world = Duel();
        StartLightAttackAndRunToFirstHit(world);
        FixedVector2 targetPosition = world.Fighter(1).Position;

        world.Tick();
        Assert.Equal(0, world.Fighter(0).HitstopFrames);
        Assert.NotEqual(targetPosition, world.Fighter(1).Position);
    }

    [Fact]
    public void InputDuringHitstopIsNotStored()
    {
        TestWorld world = Duel(rules: new MatchRulesData { HitstopEnabled = true });
        StartLightAttackAndRunToFirstHit(world);
        InputFlags before = world.Fighter(0).PrevInput;

        world.Run(3, InputFlags.Jump);
        Assert.Equal(before, world.Fighter(0).PrevInput);

        // After the hitstop, the held button counts as pressed on the first free frame.
        var context = Simulator.CreateContext(world.Fighter(0), InputFlags.Jump, world.Data);
        Assert.True(context.Pressed.Has(InputFlags.Jump));
    }

    [Fact]
    public void HitstunEndsInIdleOnTheGround()
    {
        TestWorld world = Duel();
        StartLightAttackAndRunToFirstHit(world);
        world.Run(12);
        Assert.True(world.Fighter(1).Grounded);
        Assert.Equal("Idle", world.StateName(1));
    }

    [Fact]
    public void HitstunEndsInFallInTheAir()
    {
        TestWorld world = Duel(gap: 20);
        world.Tick(Heavy | InputFlags.Up);
        Assert.Equal("Attack2Up", world.StateName(0));
        world.Run(12);                               // Heavy hitbox first active on state frame 12.
        Assert.Equal("Hitstun", world.StateName(1));

        world.Run(24);
        Assert.False(world.Fighter(1).Grounded);
        Assert.Equal("Fall", world.StateName(1));
    }

    [Fact]
    public void ArmoredStateTakesDamageButNoHitstun()
    {
        static FixedAABB Box(int minX, int minY, int maxX, int maxY) =>
            new(new FixedVector2(minX, minY), new FixedVector2(maxX, maxY));

        var builder = new FighterDefinitionBuilder(DefaultGameData.CreateFighterStats()) { IdleState = "Idle", HitstunState = "Hitstun" };
        builder.DefaultHurtbox(Box(-24, -96, 24, 0));
        builder.State("Idle", s => s
            .Movement(MovementMode.None)
            .Flags(StateFlags.ArmoredAgainstHits)
            .Transition("Punch", ConditionData.Pressed(Light)));
        builder.State("Punch", s => s
            .Duration(10, "Idle")
            .Hitbox(Box(20, -70, 64, -40), 1, 3, damage: 7, hitstunFrames: 10, knockback: new FixedVector2(5, 0), hitstopFrames: 0));
        builder.State("Hitstun", s => s.Transition("Idle", ConditionData.HitstunEnded));

        var world = new TestWorld(playerCount: 2, definition: builder.Build(), rules: NoHitstop);
        world.PlaceOnFloor(0, 400, 1);
        world.PlaceOnFloor(1, 450, -1);
        world.Tick(Light);
        world.Tick();

        Assert.Equal(93, world.Fighter(1).Health);
        Assert.Equal("Idle", world.StateName(1));
        Assert.Equal(Fixed.Zero, world.Fighter(1).Velocity.X);
    }

    [Theory]
    [InlineData(InputFlags.Attack1, "Attack1Forward")]
    [InlineData(InputFlags.Attack1 | InputFlags.Up, "Attack1Up")]
    [InlineData(InputFlags.Attack1 | InputFlags.Down, "Attack1Forward")]           // Down on solid ground: forward.
    [InlineData(InputFlags.Attack1 | InputFlags.Up | InputFlags.Down, "Attack1Forward")]
    [InlineData(InputFlags.Attack2, "Attack2Forward")]
    [InlineData(InputFlags.Attack2 | InputFlags.Up, "Attack2Up")]
    [InlineData(InputFlags.Attack2 | InputFlags.Down, "Attack2Forward")]
    public void AttackVariantOnTheFloor(InputFlags input, string expected)
    {
        var world = new TestWorld();
        world.Tick(input);
        Assert.Equal(expected, world.StateName());
    }

    [Theory]
    [InlineData(InputFlags.Attack1 | InputFlags.Down, "Attack1Down")]
    [InlineData(InputFlags.Attack2 | InputFlags.Down, "Attack2Down")]
    public void DownAttackOnAPlatform(InputFlags input, string expected)
    {
        var world = new TestWorld();
        world.PlaceOnPlatform();
        world.Tick(input);
        Assert.Equal(expected, world.StateName());
    }

    [Theory]
    [InlineData(InputFlags.Attack1 | InputFlags.Down, "Attack1Down")]
    [InlineData(InputFlags.Attack1 | InputFlags.Up, "Attack1Up")]
    [InlineData(InputFlags.Attack1, "Attack1Forward")]
    [InlineData(InputFlags.Attack2 | InputFlags.Down, "Attack2Down")]
    public void AttackVariantInTheAir(InputFlags input, string expected)
    {
        var world = new TestWorld();
        world.PlaceInAir(new FixedVector2(200, 200), FixedVector2.Zero);
        world.Tick(input);
        Assert.Equal(expected, world.StateName());
    }

    [Fact]
    public void LightAttackAllowsWalkingButKeepsTheFacing()
    {
        var world = new TestWorld();
        Fixed startX = world.Fighter().Position.X;
        world.Tick(Light);
        world.Run(5, InputFlags.Left);

        Assert.Equal("Attack1Forward", world.StateName());
        Assert.True(world.Fighter().Position.X < startX);
        Assert.Equal(1, world.Fighter().Facing);
    }

    [Fact]
    public void AirAttackKeepsTheFacing()
    {
        var world = new TestWorld();
        world.PlaceInAir(new FixedVector2(500, 100), FixedVector2.Zero);
        world.Fighter().Facing = 1;
        world.Tick(Light);
        world.Run(5, InputFlags.Left);
        Assert.Equal("Attack1Forward", world.StateName());
        Assert.Equal(1, world.Fighter().Facing);
    }

    [Fact]
    public void HeavyAttackStopsGroundMovement()
    {
        var world = new TestWorld();
        world.Run(5, InputFlags.Right);
        world.Tick(Heavy | InputFlags.Right);
        Fixed x = world.Fighter().Position.X;
        world.Run(10, InputFlags.Right);

        Assert.Equal("Attack2Forward", world.StateName());
        Assert.Equal(x, world.Fighter().Position.X);
    }

    [Fact]
    public void AttackThatEndsInTheAirGoesToFall()
    {
        var world = new TestWorld();
        world.PlaceInAir(new FixedVector2(200, 100), FixedVector2.Zero);
        world.Tick(Light);
        world.Run(18);
        Assert.False(world.Fighter().Grounded);
        Assert.Equal("Fall", world.StateName());
    }

    [Fact]
    public void AirAttackContinuesAfterLanding()
    {
        var world = new TestWorld();
        world.PlaceInAir(new FixedVector2(200, 560), FixedVector2.Zero);
        world.Tick(Heavy);
        world.RunUntilGrounded();
        Assert.Equal("Attack2Forward", world.StateName());

        world.Run(36);
        Assert.Equal("Idle", world.StateName());
    }

    [Fact]
    public void SlotOrderDoesNotChangeCombatResults()
    {
        InputFlags[] inputA = WorldDataTests.RandomInputs(seed: 31, count: 900);
        InputFlags[] inputB = WorldDataTests.RandomInputs(seed: 32, count: 900);
        var rules = new MatchRulesData { HitstopEnabled = true };

        // World 1: fighter A in slot 0, fighter B in slot 1. World 2: the same fighters in swapped slots.
        var world1 = new TestWorld(playerCount: 2, rules: rules);
        world1.PlaceOnFloor(0, 500, 1);
        world1.PlaceOnFloor(1, 560, -1);
        var world2 = new TestWorld(playerCount: 2, rules: rules);
        world2.PlaceOnFloor(1, 500, 1);
        world2.PlaceOnFloor(0, 560, -1);

        int hits = 0;
        for (int i = 0; i < inputA.Length; i++)
        {
            world1.Tick(inputA[i], inputB[i]);
            world2.Tick(inputB[i], inputA[i]);

            Assert.Equal(HashWithSwappedTargets(world1.Fighter(0)), HashOf(world2.Fighter(1)));
            Assert.Equal(HashWithSwappedTargets(world1.Fighter(1)), HashOf(world2.Fighter(0)));
            hits += world1.Fighter(0).HitTargets != 0 || world1.Fighter(1).HitTargets != 0 ? 1 : 0;
        }

        Assert.True(hits > 0, "The random inputs must cause at least one hit, or the test proves nothing.");
    }

    private static ulong HashOf(in FighterData fighter)
    {
        var hasher = new StateHasher();
        fighter.Hash(ref hasher);
        return hasher.Value;
    }

    /// <summary>The hit registry uses slot bits. With swapped slots, bits 0 and 1 swap too.</summary>
    private static ulong HashWithSwappedTargets(FighterData fighter)
    {
        int bits = fighter.HitTargets;
        fighter.HitTargets = (byte)((bits & ~0b11) | ((bits & 1) << 1) | ((bits >> 1) & 1));
        return HashOf(fighter);
    }
}
