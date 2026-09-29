using FightingGame.Core;

namespace FightingGame.Simulation;

/// <summary>Velocity control, movement, and stage collision of one fighter.</summary>
public static class FighterMovement
{
    /// <summary>Sets the velocity from the movement mode of the state, then applies gravity.</summary>
    public static void ApplyControl(ref FighterData fighter, FighterStateData state, in StateContext context)
    {
        FighterStats stats = context.Stats;
        int direction = context.Input.HorizontalDirection();
        bool canTurn = state.Has(StateFlags.CanTurn);

        switch (state.Movement)
        {
            case MovementMode.GroundControl:
                GroundControl(ref fighter, direction, canTurn, stats);
                break;
            case MovementMode.AirControl:
                AirControl(ref fighter, direction, stats);
                break;
            case MovementMode.Free:
                if (fighter.Grounded)
                {
                    GroundControl(ref fighter, direction, canTurn, stats);
                }
                else
                {
                    AirControl(ref fighter, direction, stats);
                }
                break;
            case MovementMode.Locked:
                if (fighter.Grounded)
                {
                    fighter.Velocity = fighter.Velocity.WithX(Fixed.Zero);
                }
                break;
            case MovementMode.Knockback:
                Fixed friction = fighter.Grounded ? stats.GroundFriction : stats.AirFriction;
                fighter.Velocity = fighter.Velocity.WithX(ApplyFriction(fighter.Velocity.X, friction));
                break;
            case MovementMode.None:
                break;
        }

        // Gravity also pulls a fighter on the ground. The collision stops it, and that sets Grounded.
        fighter.Velocity = fighter.Velocity.WithY(Fixed.Min(fighter.Velocity.Y + stats.Gravity, stats.MaxFallSpeed));
    }

    /// <summary>
    /// Moves the fighter and resolves collisions with the stage. Then handles a change between ground and air:
    /// the jump count, and the <see cref="FighterStateData.OnLanding"/> or <see cref="FighterStateData.OnLeaveGround"/> state.
    /// </summary>
    public static void MoveAndCollide(ref FighterData fighter, in StateContext context)
    {
        FighterStats stats = context.Stats;
        StageData stage = context.Stage;
        bool wasGrounded = fighter.Grounded;

        // X first, then Y. Each sweep checks every box and keeps the nearest stop, so the box order does not matter.
        FixedAABB box = stats.CollisionBoxAt(fighter.Position);
        Fixed dx = SweepX(box, fighter.Velocity.X, stage);
        if (dx != fighter.Velocity.X)
        {
            fighter.Velocity = fighter.Velocity.WithX(Fixed.Zero);
        }
        fighter.Position = fighter.Position.WithX(fighter.Position.X + dx);

        box = stats.CollisionBoxAt(fighter.Position);
        bool platformsBlock = fighter.DropThroughTimer == 0;
        Fixed vy = fighter.Velocity.Y;
        Fixed dy = SweepY(box, vy, stage, platformsBlock);
        bool blocked = dy != vy;
        if (blocked)
        {
            fighter.Velocity = fighter.Velocity.WithY(Fixed.Zero);
        }
        fighter.Position = fighter.Position.WithY(fighter.Position.Y + dy);
        fighter.Grounded = blocked && vy > Fixed.Zero;

        FighterStateData state = context.Definition.States[fighter.StateId];
        if (!wasGrounded && fighter.Grounded)
        {
            fighter.JumpsLeft = stats.MaxJumps;
            if (state.OnLanding != FighterStateData.NoState)
            {
                FighterStateMachine.Enter(ref fighter, state.OnLanding, context);
            }
        }
        else if (wasGrounded && !fighter.Grounded)
        {
            // A jump uses its jump before it leaves the ground. Any other way to leave the ground (walk off an edge,
            // drop through a platform) loses the ground jump.
            if (fighter.JumpsLeft > stats.MaxJumps - 1)
            {
                fighter.JumpsLeft = (byte)(stats.MaxJumps - 1);
            }
            if (state.OnLeaveGround != FighterStateData.NoState)
            {
                FighterStateMachine.Enter(ref fighter, state.OnLeaveGround, context);
            }
        }
    }

    /// <summary>True if the fighter stands on a one-way platform.</summary>
    public static bool IsOnPlatform(in FighterData fighter, in StateContext context)
    {
        if (!fighter.Grounded)
        {
            return false;
        }

        FixedAABB box = context.Stats.CollisionBoxAt(fighter.Position);
        foreach (FixedAABB platform in context.Stage.Platforms)
        {
            if (OverlapsOnX(box, platform) && platform.Min.Y == box.Max.Y)
            {
                return true;
            }
        }
        return false;
    }

    private static void GroundControl(ref FighterData fighter, int direction, bool canTurn, FighterStats stats)
    {
        if (direction != 0 && canTurn)
        {
            fighter.Facing = (sbyte)direction;
        }
        fighter.Velocity = fighter.Velocity.WithX(stats.WalkSpeed * direction);
    }

    private static void AirControl(ref FighterData fighter, int direction, FighterStats stats)
    {
        Fixed vx = direction != 0
            ? Fixed.Clamp(fighter.Velocity.X + stats.AirAcceleration * direction, -stats.AirSpeed, stats.AirSpeed)
            : ApplyFriction(fighter.Velocity.X, stats.AirFriction);
        fighter.Velocity = fighter.Velocity.WithX(vx);
    }

    /// <summary>Moves <paramref name="value"/> toward 0 by <paramref name="friction"/>, without passing 0.</summary>
    private static Fixed ApplyFriction(Fixed value, Fixed friction)
    {
        if (value > Fixed.Zero)
        {
            return Fixed.Max(value - friction, Fixed.Zero);
        }
        if (value < Fixed.Zero)
        {
            return Fixed.Min(value + friction, Fixed.Zero);
        }
        return value;
    }

    /// <summary>Returns the X distance the box can move (same sign as <paramref name="dx"/>, smaller or equal size).</summary>
    private static Fixed SweepX(FixedAABB box, Fixed dx, StageData stage)
    {
        if (dx == Fixed.Zero)
        {
            return dx;
        }

        foreach (FixedAABB solid in stage.Solids)
        {
            if (!OverlapsOnY(box, solid))
            {
                continue;
            }
            if (dx > Fixed.Zero && solid.Min.X >= box.Max.X)
            {
                dx = Fixed.Min(dx, solid.Min.X - box.Max.X);
            }
            else if (dx < Fixed.Zero && solid.Max.X <= box.Min.X)
            {
                dx = Fixed.Max(dx, solid.Max.X - box.Min.X);
            }
        }
        return dx;
    }

    /// <summary>Returns the Y distance the box can move (same sign as <paramref name="dy"/>, smaller or equal size).</summary>
    private static Fixed SweepY(FixedAABB box, Fixed dy, StageData stage, bool platformsBlock)
    {
        if (dy == Fixed.Zero)
        {
            return dy;
        }

        foreach (FixedAABB solid in stage.Solids)
        {
            if (!OverlapsOnX(box, solid))
            {
                continue;
            }
            if (dy > Fixed.Zero && solid.Min.Y >= box.Max.Y)
            {
                dy = Fixed.Min(dy, solid.Min.Y - box.Max.Y);
            }
            else if (dy < Fixed.Zero && solid.Max.Y <= box.Min.Y)
            {
                dy = Fixed.Max(dy, solid.Max.Y - box.Min.Y);
            }
        }

        if (dy > Fixed.Zero && platformsBlock)
        {
            // A platform blocks only if the feet were at or above its top before the move.
            foreach (FixedAABB platform in stage.Platforms)
            {
                if (OverlapsOnX(box, platform) && platform.Min.Y >= box.Max.Y)
                {
                    dy = Fixed.Min(dy, platform.Min.Y - box.Max.Y);
                }
            }
        }
        return dy;
    }

    private static bool OverlapsOnX(FixedAABB a, FixedAABB b) => a.Min.X < b.Max.X && b.Min.X < a.Max.X;
    private static bool OverlapsOnY(FixedAABB a, FixedAABB b) => a.Min.Y < b.Max.Y && b.Min.Y < a.Max.Y;
}
