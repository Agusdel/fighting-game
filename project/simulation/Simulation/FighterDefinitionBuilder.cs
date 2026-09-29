using System;
using System.Collections.Generic;
using System.Linq;

namespace FightingGame.Simulation;

/// <summary>
/// Builds a <see cref="FighterDefinitionData"/> in code. States refer to other states by name.
/// <see cref="Build"/> resolves the names to state ids and checks the definition.
/// </summary>
/// <example>
/// <code>
/// var builder = new FighterDefinitionBuilder(stats) { IdleState = "Idle" };
/// builder.State("Idle", s => s
///     .Movement(MovementMode.GroundControl)
///     .Flags(StateFlags.Actionable | StateFlags.CanTurn)
///     .Transition("Walk", ConditionData.DirectionHeld(DirectionInput.Horizontal)));
/// builder.SharedGroundTransition("JumpSquat", ConditionData.Pressed(InputFlags.Jump));
/// FighterDefinitionData definition = builder.Build();
/// </code>
/// </example>
public sealed class FighterDefinitionBuilder
{
    private readonly FighterStats _stats;
    private readonly List<StateBuilder> _states = new();
    private readonly List<TransitionBuilder> _sharedGround = new();
    private readonly List<TransitionBuilder> _sharedAir = new();
    private readonly List<HurtboxData> _defaultHurtboxes = new();

    public FighterDefinitionBuilder(FighterStats stats)
    {
        _stats = stats;
    }

    /// <summary>The name of the state of a fighter when it spawns.</summary>
    public string IdleState { get; set; } = "Idle";

    /// <summary>The name of the state after a hit. Null = hits do damage only (no hitstun, no knockback).</summary>
    public string? HitstunState { get; set; }

    /// <summary>The name of the state when the health reaches 0. Null = no state change.</summary>
    public string? DeadState { get; set; }

    /// <summary>Adds a hurtbox for states that do not define their own. Active on all frames.</summary>
    public FighterDefinitionBuilder DefaultHurtbox(Core.FixedAABB box)
    {
        _defaultHurtboxes.Add(new HurtboxData { Box = box, FromFrame = 0, ToFrame = int.MaxValue });
        return this;
    }

    /// <summary>Adds a state. The state id is the order of the calls (0, 1, 2, ...).</summary>
    public FighterDefinitionBuilder State(string name, Action<StateBuilder> configure)
    {
        var state = new StateBuilder(name);
        configure(state);
        _states.Add(state);
        return this;
    }

    /// <summary>Adds a shared transition for <see cref="StateFlags.Actionable"/> states while grounded.</summary>
    public FighterDefinitionBuilder SharedGroundTransition(string target, params ConditionData[] conditions)
    {
        _sharedGround.Add(new TransitionBuilder(target, conditions, 0, int.MaxValue));
        return this;
    }

    /// <summary>Adds a shared transition for <see cref="StateFlags.Actionable"/> states while airborne.</summary>
    public FighterDefinitionBuilder SharedAirTransition(string target, params ConditionData[] conditions)
    {
        _sharedAir.Add(new TransitionBuilder(target, conditions, 0, int.MaxValue));
        return this;
    }

    /// <summary>Resolves all state names and returns the definition. Throws <see cref="ArgumentException"/> with all errors.</summary>
    public FighterDefinitionData Build()
    {
        var errors = new List<string>();
        var ids = new Dictionary<string, ushort>();

        if (_states.Count >= FighterStateData.NoState)
        {
            errors.Add($"Too many states: {_states.Count}.");
        }
        for (int i = 0; i < _states.Count; i++)
        {
            if (!ids.TryAdd(_states[i].Name, (ushort)i))
            {
                errors.Add($"Two states are named '{_states[i].Name}'.");
            }
        }

        ushort Resolve(string? name, string where)
        {
            if (name == null)
            {
                return FighterStateData.NoState;
            }
            if (ids.TryGetValue(name, out ushort id))
            {
                return id;
            }
            errors.Add($"{where}: there is no state named '{name}'.");
            return FighterStateData.NoState;
        }

        TransitionData[] ResolveTransitions(List<TransitionBuilder> transitions, string where) =>
            transitions.Select(t => new TransitionData
            {
                TargetState = Resolve(t.Target, where),
                Conditions = t.Conditions,
                FromFrame = t.FromFrame,
                ToFrame = t.ToFrame,
            }).ToArray();

        var states = new FighterStateData[_states.Count];
        for (int i = 0; i < _states.Count; i++)
        {
            StateBuilder s = _states[i];
            string where = $"State '{s.Name}'";
            if (s.DurationValue > 0 && s.NextStateName == null)
            {
                errors.Add($"{where}: a state with a duration needs a next state.");
            }

            states[i] = new FighterStateData
            {
                Id = (ushort)i,
                Name = s.Name,
                Duration = s.DurationValue,
                NextState = Resolve(s.NextStateName, where),
                NextStateInAir = Resolve(s.NextStateInAirName ?? s.NextStateName, where),
                Movement = s.MovementValue,
                Flags = s.FlagsValue,
                OnLanding = Resolve(s.OnLandingName, where),
                OnLeaveGround = Resolve(s.OnLeaveGroundName, where),
                FrameActions = s.FrameActionsList.ToArray(),
                Hitboxes = s.HitboxesList.ToArray(),
                Hurtboxes = s.HurtboxesList.ToArray(),
                Transitions = ResolveTransitions(s.TransitionsList, where),
                OnEnter = s.OnEnterHook,
                OnUpdate = s.OnUpdateHook,
                OnExit = s.OnExitHook,
            };
        }

        ushort idle = Resolve(IdleState, "IdleState");
        ushort hitstun = Resolve(HitstunState, "HitstunState");
        ushort dead = Resolve(DeadState, "DeadState");
        TransitionData[] sharedGround = ResolveTransitions(_sharedGround, "Shared ground transition");
        TransitionData[] sharedAir = ResolveTransitions(_sharedAir, "Shared air transition");

        if (errors.Count > 0)
        {
            throw new ArgumentException("The fighter definition is not valid:\n  " + string.Join("\n  ", errors));
        }

        return new FighterDefinitionData
        {
            Stats = _stats,
            States = states,
            SharedGroundTransitions = sharedGround,
            SharedAirTransitions = sharedAir,
            IdleState = idle,
            HitstunState = hitstun,
            DeadState = dead,
            DefaultHurtboxes = _defaultHurtboxes.ToArray(),
        };
    }

    internal sealed record TransitionBuilder(string Target, ConditionData[] Conditions, int FromFrame, int ToFrame);

    /// <summary>Configures one state. Each method returns the same builder, so the calls can be chained.</summary>
    public sealed class StateBuilder
    {
        internal StateBuilder(string name)
        {
            Name = name;
        }

        internal string Name { get; }
        internal int DurationValue { get; private set; }
        internal string? NextStateName { get; private set; }
        internal string? NextStateInAirName { get; private set; }
        internal MovementMode MovementValue { get; private set; }
        internal StateFlags FlagsValue { get; private set; }
        internal string? OnLandingName { get; private set; }
        internal string? OnLeaveGroundName { get; private set; }
        internal List<FrameActionData> FrameActionsList { get; } = new();
        internal List<HitboxData> HitboxesList { get; } = new();
        internal List<HurtboxData> HurtboxesList { get; } = new();
        internal List<TransitionBuilder> TransitionsList { get; } = new();
        internal StateHook? OnEnterHook { get; private set; }
        internal StateHook? OnUpdateHook { get; private set; }
        internal StateHook? OnExitHook { get; private set; }

        /// <summary>
        /// The state lasts <paramref name="frames"/> frames, then changes to <paramref name="nextState"/> (grounded)
        /// or <paramref name="nextStateInAir"/> (airborne; null = the same as <paramref name="nextState"/>).
        /// </summary>
        public StateBuilder Duration(int frames, string nextState, string? nextStateInAir = null)
        {
            DurationValue = frames;
            NextStateName = nextState;
            NextStateInAirName = nextStateInAir;
            return this;
        }

        /// <summary>Adds a hitbox (relative to the feet, for a fighter that faces right), active on frames <paramref name="fromFrame"/> to <paramref name="toFrame"/>.</summary>
        public StateBuilder Hitbox(Core.FixedAABB box, int fromFrame, int toFrame, int damage, int hitstunFrames,
            Core.FixedVector2 knockback, int hitstopFrames)
        {
            HitboxesList.Add(new HitboxData
            {
                Box = box,
                FromFrame = fromFrame,
                ToFrame = toFrame,
                Damage = damage,
                HitstunFrames = hitstunFrames,
                Knockback = knockback,
                HitstopFrames = hitstopFrames,
            });
            return this;
        }

        /// <summary>Adds a hurtbox for this state. A state with its own hurtboxes does not use the default hurtboxes.</summary>
        public StateBuilder Hurtbox(Core.FixedAABB box, int fromFrame = 0, int toFrame = int.MaxValue)
        {
            HurtboxesList.Add(new HurtboxData { Box = box, FromFrame = fromFrame, ToFrame = toFrame });
            return this;
        }

        public StateBuilder Movement(MovementMode mode)
        {
            MovementValue = mode;
            return this;
        }

        public StateBuilder Flags(StateFlags flags)
        {
            FlagsValue = flags;
            return this;
        }

        public StateBuilder OnLanding(string state)
        {
            OnLandingName = state;
            return this;
        }

        public StateBuilder OnLeaveGround(string state)
        {
            OnLeaveGroundName = state;
            return this;
        }

        public StateBuilder FrameAction(int frame, FrameActionType type, Core.FixedVector2 value = default)
        {
            FrameActionsList.Add(new FrameActionData { Frame = frame, Type = type, Value = value });
            return this;
        }

        /// <summary>Adds a transition that applies on every frame of the state.</summary>
        public StateBuilder Transition(string target, params ConditionData[] conditions)
        {
            TransitionsList.Add(new TransitionBuilder(target, conditions, 0, int.MaxValue));
            return this;
        }

        /// <summary>Adds a transition that applies only on state frames <paramref name="fromFrame"/> to <paramref name="toFrame"/> (a cancel window).</summary>
        public StateBuilder TransitionInWindow(string target, int fromFrame, int toFrame, params ConditionData[] conditions)
        {
            TransitionsList.Add(new TransitionBuilder(target, conditions, fromFrame, toFrame));
            return this;
        }

        public StateBuilder OnEnter(StateHook hook)
        {
            OnEnterHook = hook;
            return this;
        }

        public StateBuilder OnUpdate(StateHook hook)
        {
            OnUpdateHook = hook;
            return this;
        }

        public StateBuilder OnExit(StateHook hook)
        {
            OnExitHook = hook;
            return this;
        }
    }
}
