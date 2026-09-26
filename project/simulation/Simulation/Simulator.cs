using FightingGame.Core;

namespace FightingGame.Simulation;

/// <summary>
/// Advances the world by one frame. The result depends only on the state, the input, and the static data,
/// so every machine that runs the same frames gets the same state.
/// </summary>
/// <remarks>
/// A tick runs in phases. Each phase runs for all fighters before the next phase starts,
/// and a phase never reads data that another fighter writes in the same phase.
/// So the player slot order cannot change the result.
/// <list type="number">
/// <item>Intent: read input, update the action, set the velocity (walk, jump, gravity).</item>
/// <item>Movement: move each fighter and resolve collisions with the stage only. Fighters do not block each other.</item>
/// </list>
/// </remarks>
public static class Simulator
{
    public static void Tick(ref WorldState state, in FrameInput input, GameData data)
    {
        for (int i = 0; i < GameConstants.MaxPlayers; i++)
        {
            ref FighterState fighter = ref state.Fighters[i];
            if (fighter.Active)
            {
                UpdateIntent(ref fighter, input[i], data);
            }
        }

        for (int i = 0; i < GameConstants.MaxPlayers; i++)
        {
            ref FighterState fighter = ref state.Fighters[i];
            if (fighter.Active)
            {
                MoveAndCollide(ref fighter, data);
            }
        }

        state.Frame++;
        state.PhaseTimer++;
    }

    // Phase 1: intent

    private static void UpdateIntent(ref FighterState fighter, InputFlags input, GameData data)
    {
        FighterStats stats = data.Fighter;
        InputFlags pressed = input & ~fighter.PrevInput;
        fighter.PrevInput = input;
        fighter.ActionFrame++;
        if (fighter.DropThroughTimer > 0)
        {
            fighter.DropThroughTimer--;
        }

        int direction = input.HorizontalDirection();

        switch (fighter.Action)
        {
            case FighterAction.Idle:
            case FighterAction.Walk:
                if (pressed.Has(InputFlags.Jump))
                {
                    if (input.Has(InputFlags.Down) && IsOnPlatform(fighter, data))
                    {
                        // Drop through: fall on the next movement. This is not a jump.
                        fighter.DropThroughTimer = stats.DropThroughFrames;
                    }
                    else
                    {
                        fighter.SetAction(FighterAction.JumpSquat);
                    }
                    break;
                }
                Walk(ref fighter, direction, stats);
                break;

            case FighterAction.JumpSquat:
                if (fighter.ActionFrame >= stats.JumpSquatFrames)
                {
                    Jump(ref fighter, stats);
                }
                break;

            case FighterAction.Airborne:
                AirControl(ref fighter, direction, stats);
                if (pressed.Has(InputFlags.Jump) && fighter.JumpsLeft > 0)
                {
                    Jump(ref fighter, stats);
                }
                break;

            case FighterAction.Land:
                fighter.Velocity = fighter.Velocity.WithX(Fixed.Zero);
                if (fighter.ActionFrame >= stats.LandFrames)
                {
                    fighter.SetAction(FighterAction.Idle);
                }
                break;
        }

        // Gravity also pulls a fighter on the ground. The movement phase stops it, and that sets Grounded.
        fighter.Velocity = fighter.Velocity.WithY(Fixed.Min(fighter.Velocity.Y + stats.Gravity, stats.MaxFallSpeed));
    }

    private static void Walk(ref FighterState fighter, int direction, FighterStats stats)
    {
        if (direction == 0)
        {
            fighter.SetAction(FighterAction.Idle);
            fighter.Velocity = fighter.Velocity.WithX(Fixed.Zero);
            return;
        }

        fighter.SetAction(FighterAction.Walk);
        fighter.Facing = (sbyte)direction;
        fighter.Velocity = fighter.Velocity.WithX(stats.WalkSpeed * direction);
    }

    private static void Jump(ref FighterState fighter, FighterStats stats)
    {
        fighter.Velocity = fighter.Velocity.WithY(stats.JumpVelocity);
        fighter.JumpsLeft--;
        fighter.SetAction(FighterAction.Airborne);
    }

    private static void AirControl(ref FighterState fighter, int direction, FighterStats stats)
    {
        Fixed vx = fighter.Velocity.X;
        if (direction != 0)
        {
            vx = Fixed.Clamp(vx + stats.AirAcceleration * direction, -stats.AirSpeed, stats.AirSpeed);
        }
        else if (vx > Fixed.Zero)
        {
            vx = Fixed.Max(vx - stats.AirFriction, Fixed.Zero);
        }
        else if (vx < Fixed.Zero)
        {
            vx = Fixed.Min(vx + stats.AirFriction, Fixed.Zero);
        }
        fighter.Velocity = fighter.Velocity.WithX(vx);
    }

    // Phase 2: movement and stage collision

    private static void MoveAndCollide(ref FighterState fighter, GameData data)
    {
        FighterStats stats = data.Fighter;
        StageData stage = data.Stage;

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

        UpdateGroundedAction(ref fighter, stats);
    }

    private static void UpdateGroundedAction(ref FighterState fighter, FighterStats stats)
    {
        bool groundAction = fighter.Action != FighterAction.Airborne;
        if (fighter.Grounded && !groundAction)
        {
            fighter.SetAction(FighterAction.Land);
            fighter.JumpsLeft = stats.MaxJumps;
        }
        else if (!fighter.Grounded && groundAction)
        {
            // Walked off an edge or dropped through a platform: the ground jump is lost.
            fighter.SetAction(FighterAction.Airborne);
            if (fighter.JumpsLeft > stats.MaxJumps - 1)
            {
                fighter.JumpsLeft = (byte)(stats.MaxJumps - 1);
            }
        }
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

    private static bool IsOnPlatform(in FighterState fighter, GameData data)
    {
        if (!fighter.Grounded)
        {
            return false;
        }

        FixedAABB box = data.Fighter.CollisionBoxAt(fighter.Position);
        foreach (FixedAABB platform in data.Stage.Platforms)
        {
            if (OverlapsOnX(box, platform) && platform.Min.Y == box.Max.Y)
            {
                return true;
            }
        }
        return false;
    }

    private static bool OverlapsOnX(FixedAABB a, FixedAABB b) => a.Min.X < b.Max.X && b.Min.X < a.Max.X;
    private static bool OverlapsOnY(FixedAABB a, FixedAABB b) => a.Min.Y < b.Max.Y && b.Min.Y < a.Max.Y;
}
