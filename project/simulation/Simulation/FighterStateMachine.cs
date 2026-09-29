namespace FightingGame.Simulation;

/// <summary>
/// Runs the state machine of one fighter. The current state is only <see cref="FighterData.StateId"/> and
/// <see cref="FighterData.StateFrame"/>; the states themselves are static data. So a rollback (a copy of
/// <see cref="FighterData"/>) also restores the state machine.
/// </summary>
/// <remarks>
/// Order on each frame (<see cref="Update"/>):
/// <list type="number">
/// <item><c>StateFrame</c> advances by 1.</item>
/// <item>Duration end: if the state has a duration and it is over, change to the next state.</item>
/// <item>Otherwise, one rule transition: first the shared transitions (if the state is <see cref="StateFlags.Actionable"/>:
/// the ground list when grounded, the air list when airborne), then the state's own transitions.
/// The first transition with all conditions true, inside its frame window, wins.</item>
/// <item>The frame actions of the current state for the current frame.</item>
/// <item>The <see cref="FighterStateData.OnUpdate"/> hook.</item>
/// <item>The movement mode sets the velocity, then gravity applies.</item>
/// </list>
/// Shared transitions come first, so an action (jump, attack) has priority over a movement change (Idle to Walk)
/// on the same frame. Landing and leaving the ground are handled after the collision, in <see cref="FighterMovement"/>.
/// At most one transition happens per frame. The rules of a state entered by a duration end are checked from the next
/// frame: <see cref="FighterData.Grounded"/> is from the last collision, so on that frame it can be wrong for the new
/// state (for example on the first frame of a jump).
/// </remarks>
public static class FighterStateMachine
{
    public static void Update(ref FighterData fighter, in StateContext context)
    {
        FighterDefinitionData definition = context.Definition;

        if (fighter.DropThroughTimer > 0)
        {
            fighter.DropThroughTimer--;
        }

        fighter.StateFrame++;
        FighterStateData state = definition.States[fighter.StateId];

        if (state.Duration > 0 && fighter.StateFrame >= state.Duration)
        {
            Enter(ref fighter, fighter.Grounded ? state.NextState : state.NextStateInAir, context);
            state = definition.States[fighter.StateId];
        }
        else if (TryFindTransition(fighter, state, context, out ushort target))
        {
            Enter(ref fighter, target, context);
            state = definition.States[fighter.StateId];
        }

        RunFrameActions(ref fighter, state, context);
        state.OnUpdate?.Invoke(ref fighter, context);
        FighterMovement.ApplyControl(ref fighter, state, context);
    }

    /// <summary>
    /// Changes the state: runs <see cref="FighterStateData.OnExit"/> of the old state, resets the state values,
    /// then runs <see cref="FighterStateData.OnEnter"/> of the new state. A change to the current state restarts it.
    /// </summary>
    public static void Enter(ref FighterData fighter, ushort stateId, in StateContext context)
    {
        context.Definition.States[fighter.StateId].OnExit?.Invoke(ref fighter, context);
        EnterWithoutExit(ref fighter, stateId, context);
    }

    /// <summary>Sets the first state of a new fighter (there is no old state to exit).</summary>
    public static void EnterInitial(ref FighterData fighter, ushort stateId, in StateContext context)
    {
        EnterWithoutExit(ref fighter, stateId, context);
    }

    private static void EnterWithoutExit(ref FighterData fighter, ushort stateId, in StateContext context)
    {
        fighter.StateId = stateId;
        fighter.StateFrame = 0;
        fighter.StateVar0 = 0;
        fighter.StateVar1 = 0;
        fighter.StateVar2 = 0;
        fighter.StateVar3 = 0;
        fighter.HitTargets = 0;
        context.Definition.States[stateId].OnEnter?.Invoke(ref fighter, context);
    }

    private static bool TryFindTransition(in FighterData fighter, FighterStateData state, in StateContext context, out ushort target)
    {
        if (state.Has(StateFlags.Actionable))
        {
            TransitionData[] shared = fighter.Grounded
                ? context.Definition.SharedGroundTransitions
                : context.Definition.SharedAirTransitions;
            if (TryFindTransition(fighter, shared, context, out target))
            {
                return true;
            }
        }
        return TryFindTransition(fighter, state.Transitions, context, out target);
    }

    private static bool TryFindTransition(in FighterData fighter, TransitionData[] transitions, in StateContext context, out ushort target)
    {
        foreach (TransitionData transition in transitions)
        {
            if (fighter.StateFrame < transition.FromFrame || fighter.StateFrame > transition.ToFrame)
            {
                continue;
            }

            bool match = true;
            foreach (ConditionData condition in transition.Conditions)
            {
                if (!IsTrue(condition, fighter, context))
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                target = transition.TargetState;
                return true;
            }
        }

        target = FighterStateData.NoState;
        return false;
    }

    public static bool IsTrue(in ConditionData condition, in FighterData fighter, in StateContext context)
    {
        switch (condition.Type)
        {
            case ConditionType.InputPressed:
                return (context.Pressed & condition.Buttons) == condition.Buttons;
            case ConditionType.InputHeld:
                return (context.Input & condition.Buttons) == condition.Buttons;
            case ConditionType.InputReleased:
                return (context.Released & condition.Buttons) == condition.Buttons;
            case ConditionType.Direction:
                return IsDirectionHeld(condition.Direction, fighter, context.Input);
            case ConditionType.Grounded:
                return fighter.Grounded;
            case ConditionType.Airborne:
                return !fighter.Grounded;
            case ConditionType.OnPlatform:
                return FighterMovement.IsOnPlatform(fighter, context);
            case ConditionType.VelocityYDown:
                return fighter.Velocity.Y > Core.Fixed.Zero;
            case ConditionType.HasJumpsLeft:
                return fighter.JumpsLeft > 0;
            case ConditionType.HitstunEnded:
                return fighter.StateFrame >= fighter.HitstunFrames;
            case ConditionType.Custom:
                return condition.Custom!(fighter, context);
            default:
                return false;
        }
    }

    private static bool IsDirectionHeld(DirectionInput direction, in FighterData fighter, InputFlags input)
    {
        bool up = input.Has(InputFlags.Up);
        bool down = input.Has(InputFlags.Down);
        int horizontal = input.HorizontalDirection();

        return direction switch
        {
            DirectionInput.Up => up && !down,
            DirectionInput.Down => down && !up,
            DirectionInput.Forward => horizontal != 0 && horizontal == fighter.Facing,
            DirectionInput.Back => horizontal != 0 && horizontal == -fighter.Facing,
            DirectionInput.Horizontal => horizontal != 0,
            DirectionInput.NoHorizontal => horizontal == 0,
            _ => false,
        };
    }

    private static void RunFrameActions(ref FighterData fighter, FighterStateData state, in StateContext context)
    {
        foreach (FrameActionData action in state.FrameActions)
        {
            if (action.Frame != fighter.StateFrame)
            {
                continue;
            }

            FighterStats stats = context.Stats;
            Core.Fixed forwardX = action.Value.X * fighter.Facing;
            switch (action.Type)
            {
                case FrameActionType.Jump:
                    fighter.Velocity = fighter.Velocity.WithY(stats.JumpVelocity);
                    if (fighter.JumpsLeft > 0)
                    {
                        fighter.JumpsLeft--;
                    }
                    break;
                case FrameActionType.StartDropThrough:
                    fighter.DropThroughTimer = stats.DropThroughFrames;
                    break;
                case FrameActionType.SetVelocity:
                    fighter.Velocity = new Core.FixedVector2(forwardX, action.Value.Y);
                    break;
                case FrameActionType.SetVelocityX:
                    fighter.Velocity = fighter.Velocity.WithX(forwardX);
                    break;
                case FrameActionType.SetVelocityY:
                    fighter.Velocity = fighter.Velocity.WithY(action.Value.Y);
                    break;
            }
        }
    }
}
