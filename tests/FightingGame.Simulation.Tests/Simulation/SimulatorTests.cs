using FightingGame.Core;
using FightingGame.Simulation;
using Xunit;

namespace FightingGame.Simulation.Tests.Simulation;

public class SimulatorTests
{
    // Default stage: floor top y = 600, ceiling bottom y = 32, left wall right edge x = 32, right wall left edge x = 1120.
    // Platform: x 426..726, top y = 420. Fighter collision box: 48 x 96.
    private static readonly Fixed FloorY = 600;
    private static readonly Fixed PlatformY = 420;

    [Fact]
    public void StandingFighterStaysOnTheFloor()
    {
        var world = new TestWorld();
        FixedVector2 start = world.Fighter().Position;
        world.Run(120);

        Assert.Equal(start, world.Fighter().Position);
        Assert.True(world.Fighter().Grounded);
        Assert.Equal("Idle", world.StateName());
        Assert.Equal(120, world.State.Frame);
    }

    [Fact]
    public void WalkMovesAtWalkSpeedAndSetsFacing()
    {
        var world = new TestWorld();
        Fixed startX = world.Fighter().Position.X;

        world.Run(10, InputFlags.Left);

        Assert.Equal(startX - world.Stats.WalkSpeed * 10, world.Fighter().Position.X);
        Assert.Equal(-1, world.Fighter().Facing);
        Assert.Equal("Walk", world.StateName());
        Assert.Equal(9, world.Fighter().StateFrame);
    }

    [Fact]
    public void StopsWhenNoDirectionIsHeld()
    {
        var world = new TestWorld();
        world.Run(5, InputFlags.Right);
        Fixed x = world.Fighter().Position.X;
        world.Run(5);

        Assert.Equal(x, world.Fighter().Position.X);
        Assert.Equal("Idle", world.StateName());
    }

    [Fact]
    public void LeftAndRightTogetherDoNotMove()
    {
        var world = new TestWorld();
        FixedVector2 start = world.Fighter().Position;
        world.Run(10, InputFlags.Left | InputFlags.Right);
        Assert.Equal(start, world.Fighter().Position);
    }

    [Fact]
    public void WallsStopTheFighterExactly()
    {
        var world = new TestWorld();
        world.Run(300, InputFlags.Right);
        Assert.Equal((Fixed)(1120 - 24), world.Fighter().Position.X);
        Assert.Equal(Fixed.Zero, world.Fighter().Velocity.X);

        world.Run(300, InputFlags.Left);
        Assert.Equal((Fixed)(32 + 24), world.Fighter().Position.X);
    }

    [Fact]
    public void JumpHasJumpSquatThenRisesThenLandsOnTheFloor()
    {
        var world = new TestWorld();
        int squat = world.StateDuration("JumpSquat");

        world.Tick(InputFlags.Jump);
        Assert.Equal("JumpSquat", world.StateName());
        world.Run(squat - 1, InputFlags.Jump);
        Assert.Equal("JumpSquat", world.StateName());
        Assert.Equal(FloorY, world.Fighter().Position.Y);

        world.Tick(InputFlags.Jump);
        Assert.Equal("Jump", world.StateName());
        Assert.True(world.Fighter().Position.Y < FloorY);
        Assert.Equal(1, world.Fighter().JumpsLeft);

        world.RunUntilGrounded();
        Assert.Equal(FloorY, world.Fighter().Position.Y);
        Assert.Equal("Land", world.StateName());
        Assert.Equal(world.Stats.MaxJumps, world.Fighter().JumpsLeft);

        world.Run(world.StateDuration("Land"));
        Assert.Equal("Idle", world.StateName());
    }

    [Fact]
    public void HoldingJumpDoesNotJumpAgain()
    {
        var world = new TestWorld();
        world.Run(20, InputFlags.Jump);
        Assert.Equal(1, world.Fighter().JumpsLeft);
    }

    [Fact]
    public void DoubleJumpWorksOnceInTheAir()
    {
        var world = new TestWorld();
        world.Run(10, InputFlags.Jump);
        world.Tick();

        world.Tick(InputFlags.Jump);
        Assert.Equal(0, world.Fighter().JumpsLeft);
        Fixed vyAfterDoubleJump = world.Fighter().Velocity.Y;
        Assert.True(vyAfterDoubleJump < Fixed.Zero);

        world.Tick();
        world.Tick(InputFlags.Jump);
        Assert.Equal(0, world.Fighter().JumpsLeft);
        Assert.Equal(vyAfterDoubleJump + world.Stats.Gravity * 2, world.Fighter().Velocity.Y);
    }

    [Fact]
    public void FighterCanTurnInTheAir()
    {
        var world = new TestWorld();
        world.PlaceInAir(new FixedVector2(500, 200), FixedVector2.Zero);
        world.Fighter().Facing = 1;
        world.Tick(InputFlags.Left);
        Assert.Equal("Fall", world.StateName());
        Assert.Equal(-1, world.Fighter().Facing);
    }

    [Fact]
    public void FallSpeedIsLimited()
    {
        var world = new TestWorld();
        world.PlaceInAir(new FixedVector2(200, 200), FixedVector2.Zero);
        // About 21 frames to reach the maximum speed; the fighter lands after about 43 frames.
        world.Run(25);
        Assert.False(world.Fighter().Grounded);
        Assert.Equal(world.Stats.MaxFallSpeed, world.Fighter().Velocity.Y);
    }

    [Fact]
    public void CeilingStopsTheFighterExactly()
    {
        var world = new TestWorld();
        world.PlaceInAir(new FixedVector2(200, 140), new FixedVector2(0, -20));
        world.Tick();

        Assert.Equal((Fixed)(32 + 96), world.Fighter().Position.Y);
        Assert.Equal(Fixed.Zero, world.Fighter().Velocity.Y);
        Assert.False(world.Fighter().Grounded);
    }

    [Fact]
    public void FallingFighterLandsOnThePlatform()
    {
        var world = new TestWorld();
        world.PlaceInAir(new FixedVector2(576, 200), FixedVector2.Zero);
        world.RunUntilGrounded();

        Assert.Equal(PlatformY, world.Fighter().Position.Y);
        Assert.Equal("Land", world.StateName());
    }

    [Fact]
    public void RisingFighterPassesThroughThePlatform()
    {
        var world = new TestWorld();
        world.PlaceInAir(new FixedVector2(576, 460), new FixedVector2(0, -10));
        world.Tick();

        // Velocity -10 + gravity 0.6 = -9.4: the platform does not stop an upward move.
        Assert.Equal(Fixed.FromRatio(4506, 10), world.Fighter().Position.Y);
    }

    [Fact]
    public void FighterBelowThePlatformTopFallsThrough()
    {
        // The feet start below the platform top, so the platform must not catch the fighter.
        var world = new TestWorld();
        world.PlaceInAir(new FixedVector2(576, 425), FixedVector2.Zero);
        world.RunUntilGrounded();
        Assert.Equal(FloorY, world.Fighter().Position.Y);
    }

    [Fact]
    public void DownAndJumpDropsThroughThePlatform()
    {
        var world = new TestWorld();
        world.PlaceOnPlatform();
        Assert.Equal(PlatformY, world.Fighter().Position.Y);
        Assert.Equal("Idle", world.StateName());

        world.Tick(InputFlags.Down | InputFlags.Jump);
        world.Tick(InputFlags.Down | InputFlags.Jump);

        Assert.True(world.Fighter().Position.Y > PlatformY);
        Assert.Equal("Fall", world.StateName());
        Assert.True(world.Fighter().Velocity.Y > Fixed.Zero, "A drop-through is not a jump.");
        Assert.Equal(1, world.Fighter().JumpsLeft);

        world.RunUntilGrounded();
        Assert.Equal(FloorY, world.Fighter().Position.Y);
    }

    [Fact]
    public void DownAndJumpOnTheFloorIsANormalJump()
    {
        var world = new TestWorld();
        world.Tick(InputFlags.Down | InputFlags.Jump);
        Assert.Equal("JumpSquat", world.StateName());
    }

    [Fact]
    public void WalkingOffThePlatformLosesTheGroundJump()
    {
        var world = new TestWorld();
        world.PlaceOnPlatform();

        for (int i = 0; i < 60 && world.Fighter().Grounded; i++)
        {
            world.Tick(InputFlags.Right);
        }

        Assert.False(world.Fighter().Grounded);
        Assert.Equal("Fall", world.StateName());
        Assert.Equal(1, world.Fighter().JumpsLeft);
    }

    [Fact]
    public void InactiveFightersDoNotChange()
    {
        var world = new TestWorld(playerCount: 1);
        FighterData before = world.Fighter(3);
        world.Run(60, InputFlags.Right);

        var hasherBefore = new StateHasher();
        before.Hash(ref hasherBefore);
        var hasherAfter = new StateHasher();
        world.Fighter(3).Hash(ref hasherAfter);
        Assert.Equal(hasherBefore.Value, hasherAfter.Value);
    }

    [Fact]
    public void PlayerSlotOrderDoesNotChangeTheResult()
    {
        InputFlags[] inputA = WorldDataTests.RandomInputs(seed: 10, count: 600);
        InputFlags[] inputB = WorldDataTests.RandomInputs(seed: 20, count: 600);

        // World 1: fighter A in slot 0, fighter B in slot 1.
        var world1 = new TestWorld(playerCount: 2);
        // World 2: the same fighters in swapped slots.
        var world2 = new TestWorld(playerCount: 2);
        (world2.State.Fighters[0], world2.State.Fighters[1]) = (world2.State.Fighters[1], world2.State.Fighters[0]);

        for (int i = 0; i < inputA.Length; i++)
        {
            world1.Tick(inputA[i], inputB[i]);
            world2.Tick(inputB[i], inputA[i]);

            Assert.Equal(HashOf(world1.Fighter(0)), HashOf(world2.Fighter(1)));
            Assert.Equal(HashOf(world1.Fighter(1)), HashOf(world2.Fighter(0)));
        }
    }

    private static ulong HashOf(in FighterData fighter)
    {
        var hasher = new StateHasher();
        fighter.Hash(ref hasher);
        return hasher.Value;
    }
}
