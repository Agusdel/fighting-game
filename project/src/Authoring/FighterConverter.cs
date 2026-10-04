using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FightingGame.Core;
using FightingGame.Simulation;
using Godot;

namespace FightingGame.Authoring;

/// <summary>
/// Converts a fighter scene (a <see cref="FighterRoot"/> and the authoring nodes below it) into
/// <see cref="FighterDefinitionData"/> with exact fixed-point values.
/// </summary>
/// <remarks>
/// Determinism rules:
/// <list type="bullet">
/// <item>Box positions and sizes must be whole pixels (the same rule as the stage). A position relative to the feet is
/// the sum of the local positions from the node up to the root, in <see cref="int"/> math. Every node in that chain must
/// be a <see cref="Node2D"/> with a whole-pixel position, no rotation, scale (1, 1), and no skew.</item>
/// <item>Values with fractions (stats, knockback, frame action velocities) are converted from the decimal text of the
/// float, which is the value as it was typed (0.6, not 0.60000002), and rounded toward zero, the same as
/// <see cref="Fixed.FromRatio"/>. The shortest round-trip text of a float is exact and the same on every machine.</item>
/// <item>State ids are the tree order of the <see cref="FighterState"/> nodes. All lists use the child order, which is
/// saved in the scene file.</item>
/// <item>The state animation (same name as the state) gives the boxes of each frame. The converter reads the keys of the
/// box tracks directly; it never samples the animation. Box tracks must be discrete, so the editor preview shows the
/// same boxes as the game. On a frame before the first key, a box has its default value: the key of the "RESET"
/// animation if there is one, else the value in the scene.</item>
/// <item>Consecutive frames with the same box become one <see cref="HitboxData"/> or <see cref="HurtboxData"/> with a
/// frame range. A state with no duration has one box for all frames, so its box tracks must be constant.</item>
/// </list>
/// The converter does not trust the editor: it checks all values again, and collects all errors.
/// </remarks>
public static class FighterConverter
{
    /// <summary>A float must be closer than this to a whole number (pixels, frames).</summary>
    private const double IntegerTolerance = 0.001;

    private const string ResetAnimation = "RESET";
    private const string PositionProperty = "position";

    /// <summary>Converts the fighter. Throws <see cref="FighterConversionException"/> with all errors if the fighter is not valid.</summary>
    public static FighterDefinitionData Convert(FighterRoot root, StateHookRegistry? registry = null)
    {
        List<string> errors = TryConvert(root, out FighterDefinitionData? data, registry);
        if (data == null)
        {
            throw new FighterConversionException(root.SceneFilePath, errors);
        }
        return data;
    }

    /// <summary>Converts the fighter. Returns the list of errors (empty on success). <paramref name="data"/> is null if there are errors.</summary>
    public static List<string> TryConvert(FighterRoot root, out FighterDefinitionData? data, StateHookRegistry? registry = null)
    {
        var context = new Context(root, registry ?? StateHookRegistry.Default);
        data = context.Convert();
        // A problem can be found from two nodes (for example a state and its hitbox): report it once.
        return context.Errors.Distinct().ToList();
    }

    /// <summary>
    /// Converts an authored float to fixed point: the shortest decimal text of the float (the typed value), times 65536,
    /// rounded toward zero. Returns false if the value is not finite or too large.
    /// </summary>
    public static bool TryToFixed(float value, out Fixed result)
    {
        result = Fixed.Zero;
        if (!float.IsFinite(value))
        {
            return false;
        }
        string text = value.ToString("R", CultureInfo.InvariantCulture);
        if (!decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal exact))
        {
            return false;
        }
        try
        {
            decimal raw = decimal.Truncate(exact * Fixed.OneRaw);
            if (raw > int.MaxValue * (decimal)Fixed.OneRaw || raw < int.MinValue * (decimal)Fixed.OneRaw)
            {
                return false;
            }
            result = Fixed.FromRaw((long)raw);
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    /// <summary>The value of one box on one frame.</summary>
    private readonly record struct BoxFrame(bool Active, Vector2I Position, Vector2I Size);

    /// <summary>The keys of one property of one box node in one animation, as (frame, value), sorted by frame.</summary>
    private sealed class PropertyKeys
    {
        public readonly List<(int Frame, Variant Value)> Keys = new();

        public bool TryGetValueAt(int frame, out Variant value)
        {
            value = default;
            bool found = false;
            foreach ((int keyFrame, Variant keyValue) in Keys)
            {
                if (keyFrame > frame)
                {
                    break;
                }
                value = keyValue;
                found = true;
            }
            return found;
        }
    }

    /// <summary>The keys of the box tracks of one state animation: node → property → keys.</summary>
    private sealed class BoxTracks
    {
        public readonly Dictionary<Node, Dictionary<string, PropertyKeys>> Nodes = new();

        public bool HasKeysFor(Node node) => Nodes.ContainsKey(node);

        public PropertyKeys? Get(Node node, string property) =>
            Nodes.TryGetValue(node, out Dictionary<string, PropertyKeys>? properties) && properties.TryGetValue(property, out PropertyKeys? keys)
                ? keys
                : null;
    }

    private sealed class Context
    {
        private readonly FighterRoot _root;
        private readonly StateHookRegistry _registry;
        private readonly Dictionary<string, ushort> _stateIds = new();
        private AnimationPlayer? _player;
        private Node? _trackRoot;

        public readonly List<string> Errors = new();

        public Context(FighterRoot root, StateHookRegistry registry)
        {
            _root = root;
            _registry = registry;
        }

        public FighterDefinitionData? Convert()
        {
            _player = FindAnimationPlayer();
            _trackRoot = _player?.GetNodeOrNull(_player.RootNode);
            if (_player != null && _trackRoot == null)
            {
                Errors.Add($"AnimationPlayer: the root node '{_player.RootNode}' does not exist.");
            }

            List<FighterState> stateNodes = FighterRoot.FindAll<FighterState>(_root);
            if (stateNodes.Count >= FighterStateData.NoState)
            {
                Errors.Add($"Too many states: {stateNodes.Count}.");
            }
            for (int i = 0; i < stateNodes.Count; i++)
            {
                if (!_stateIds.TryAdd(stateNodes[i].Name, (ushort)i))
                {
                    Errors.Add($"Two states are named '{stateNodes[i].Name}'. State names must be unique.");
                }
            }

            FighterStats? stats = ConvertStats();
            HurtboxData[] defaultHurtboxes = ConvertDefaultHurtboxes();
            var states = new FighterStateData[stateNodes.Count];
            for (int i = 0; i < stateNodes.Count; i++)
            {
                states[i] = ConvertState(stateNodes[i], (ushort)i);
            }

            var sharedGround = new List<TransitionData>();
            var sharedAir = new List<TransitionData>();
            foreach (FighterSharedTransitions list in FighterRoot.FindAll<FighterSharedTransitions>(_root))
            {
                List<TransitionData> target = list.Type == FighterSharedTransitionType.Ground ? sharedGround : sharedAir;
                target.AddRange(ConvertTransitions(list));
            }
            CheckPlacement();

            if (_root.IdleState.Length == 0)
            {
                Errors.Add("Select the IdleState (the state of the fighter when it spawns).");
            }
            ushort idle = ResolveState(_root.IdleState, nameof(FighterRoot.IdleState));
            ushort hitstun = ResolveState(_root.HitstunState, nameof(FighterRoot.HitstunState));
            ushort dead = ResolveState(_root.DeadState, nameof(FighterRoot.DeadState));

            if (Errors.Count > 0 || stats == null)
            {
                return null;
            }
            return new FighterDefinitionData
            {
                Stats = stats,
                States = states,
                SharedGroundTransitions = sharedGround.ToArray(),
                SharedAirTransitions = sharedAir.ToArray(),
                IdleState = idle,
                HitstunState = hitstun,
                DeadState = dead,
                DefaultHurtboxes = defaultHurtboxes,
            };
        }

        private FighterStats? ConvertStats()
        {
            List<FighterCollisionBox> boxes = FighterRoot.FindAll<FighterCollisionBox>(_root);
            if (boxes.Count != 1)
            {
                Errors.Add($"A fighter needs exactly one FighterCollisionBox (it has {boxes.Count}).");
                return null;
            }
            FighterCollisionBox box = boxes[0];
            if (box.GetParent() != _root || box.Position != Vector2.Zero)
            {
                Errors.Add($"{PathOf(box)}: the collision box must be a direct child of the root, at position (0, 0).");
            }
            if (box.Size.X % 2 != 0)
            {
                Errors.Add($"{PathOf(box)}: use an even width, so the box is centered exactly on the feet.");
            }

            if (_root.MaxJumps is < 1 or > byte.MaxValue)
            {
                Errors.Add($"MaxJumps must be 1 to {byte.MaxValue}.");
            }
            return new FighterStats
            {
                CollisionBoxSize = new FixedVector2(Fixed.FromInt(box.Size.X), Fixed.FromInt(box.Size.Y)),
                WalkSpeed = ToFixed(_root.WalkSpeed, nameof(FighterRoot.WalkSpeed)),
                AirSpeed = ToFixed(_root.AirSpeed, nameof(FighterRoot.AirSpeed)),
                AirAcceleration = ToFixed(_root.AirAcceleration, nameof(FighterRoot.AirAcceleration)),
                AirFriction = ToFixed(_root.AirFriction, nameof(FighterRoot.AirFriction)),
                Gravity = ToFixed(_root.Gravity, nameof(FighterRoot.Gravity)),
                MaxFallSpeed = ToFixed(_root.MaxFallSpeed, nameof(FighterRoot.MaxFallSpeed)),
                JumpVelocity = ToFixed(_root.JumpVelocity, nameof(FighterRoot.JumpVelocity)),
                MaxJumps = (byte)Math.Clamp(_root.MaxJumps, 1, byte.MaxValue),
                DropThroughFrames = _root.DropThroughFrames,
                MaxHealth = _root.MaxHealth,
                GroundFriction = ToFixed(_root.GroundFriction, nameof(FighterRoot.GroundFriction)),
            };
        }

        /// <summary>The hurtboxes outside the states, with their scene values, for all frames.</summary>
        private HurtboxData[] ConvertDefaultHurtboxes()
        {
            var result = new List<HurtboxData>();
            foreach (FighterHurtbox hurtbox in DefaultHurtboxNodes())
            {
                if (TryGetSceneBox(hurtbox, hurtbox.Size, hurtbox.Active, out BoxFrame box) && box.Active)
                {
                    result.Add(new HurtboxData { Box = ToAabb(box), FromFrame = 0, ToFrame = int.MaxValue });
                }
            }
            if (result.Count == 0)
            {
                Errors.Add("The fighter has no active FighterHurtbox: it can never be hit.");
            }
            return result.ToArray();
        }

        private FighterStateData ConvertState(FighterState node, ushort id)
        {
            string where = $"State '{node.Name}'";
            if (node.Duration < 0)
            {
                Errors.Add($"{where}: the duration must not be negative.");
            }
            int duration = Math.Max(0, node.Duration);
            if (duration > 0 && node.NextState.Length == 0)
            {
                Errors.Add($"{where}: a state with a duration needs a NextState.");
            }
            CheckChain(node);

            BoxTracks tracks = ReadBoxTracks(node, duration);
            string nextInAir = node.NextStateInAir.Length > 0 ? node.NextStateInAir : node.NextState;

            var frameActions = new List<FrameActionData>();
            var hitboxes = new List<HitboxData>();
            var transitions = new List<TransitionData>();
            foreach (Node child in node.GetChildren())
            {
                switch (child)
                {
                    case FighterHitbox hitbox:
                        AddHitboxRanges(hitbox, tracks, duration, hitboxes);
                        break;
                    case FighterFrameAction action:
                        frameActions.Add(ConvertFrameAction(action, duration, where));
                        break;
                }
            }
            transitions.AddRange(ConvertTransitions(node));

            return new FighterStateData
            {
                Id = id,
                Name = node.Name,
                Duration = duration,
                NextState = ResolveState(node.NextState, $"{where} NextState"),
                NextStateInAir = ResolveState(nextInAir, $"{where} NextStateInAir"),
                Movement = node.Movement,
                Flags = node.Flags,
                OnLanding = ResolveState(node.OnLanding, $"{where} OnLanding"),
                OnLeaveGround = ResolveState(node.OnLeaveGround, $"{where} OnLeaveGround"),
                FrameActions = frameActions.ToArray(),
                Hitboxes = hitboxes.ToArray(),
                Hurtboxes = ConvertStateHurtboxes(tracks, duration, where),
                Transitions = transitions.ToArray(),
                OnEnter = ResolveHook(node.OnEnter, $"{where} OnEnter"),
                OnUpdate = ResolveHook(node.OnUpdate, $"{where} OnUpdate"),
                OnExit = ResolveHook(node.OnExit, $"{where} OnExit"),
            };
        }

        /// <summary>The frame ranges of one hitbox: one entry for each run of frames with the same active box.</summary>
        private void AddHitboxRanges(FighterHitbox hitbox, BoxTracks tracks, int duration, List<HitboxData> result)
        {
            if (!TryGetSceneBox(hitbox, hitbox.Size, hitbox.Active, out BoxFrame sceneBox))
            {
                return;
            }
            Fixed knockbackX = ToFixed(hitbox.Knockback.X, $"{PathOf(hitbox)} Knockback.X");
            Fixed knockbackY = ToFixed(hitbox.Knockback.Y, $"{PathOf(hitbox)} Knockback.Y");
            foreach ((BoxFrame box, int from, int to) in FrameRuns(hitbox, sceneBox, tracks, duration))
            {
                if (box.Active)
                {
                    result.Add(new HitboxData
                    {
                        Box = ToAabb(box),
                        FromFrame = from,
                        ToFrame = to,
                        Damage = hitbox.Damage,
                        HitstunFrames = hitbox.HitstunFrames,
                        Knockback = new FixedVector2(knockbackX, knockbackY),
                        HitstopFrames = hitbox.HitstopFrames,
                    });
                }
            }
        }

        /// <summary>
        /// The hurtboxes of a state. Empty (= the default hurtboxes) if the state animation does not key any hurtbox.
        /// </summary>
        private HurtboxData[] ConvertStateHurtboxes(BoxTracks tracks, int duration, string where)
        {
            List<FighterHurtbox> hurtboxes = DefaultHurtboxNodes();
            bool keyed = false;
            foreach (FighterHurtbox hurtbox in hurtboxes)
            {
                keyed |= tracks.HasKeysFor(hurtbox);
            }
            if (!keyed)
            {
                return Array.Empty<HurtboxData>();
            }

            var result = new List<HurtboxData>();
            foreach (FighterHurtbox hurtbox in hurtboxes)
            {
                if (!TryGetSceneBox(hurtbox, hurtbox.Size, hurtbox.Active, out BoxFrame sceneBox))
                {
                    continue;
                }
                foreach ((BoxFrame box, int from, int to) in FrameRuns(hurtbox, sceneBox, tracks, duration))
                {
                    if (box.Active)
                    {
                        result.Add(new HurtboxData { Box = ToAabb(box), FromFrame = from, ToFrame = to });
                    }
                }
            }
            if (result.Count == 0)
            {
                // An empty list means "the default hurtboxes" in the simulation.
                Errors.Add($"{where}: the animation turns off all hurtboxes. Use the Intangible flag for a state that cannot be hit.");
            }
            return result.ToArray();
        }

        /// <summary>
        /// The value of a box on each frame of a state, as runs of equal frames: (box, first frame, last frame).
        /// A state with no duration has one run for all frames (its keys must all give the same box).
        /// </summary>
        private List<(BoxFrame Box, int From, int To)> FrameRuns(SnappedNode2D node, BoxFrame sceneBox, BoxTracks tracks, int duration)
        {
            var runs = new List<(BoxFrame, int, int)>();
            BoxFrame defaults = DefaultBox(node, sceneBox);
            if (duration == 0)
            {
                BoxFrame first = BoxAt(node, defaults, tracks, 0);
                foreach (int keyFrame in KeyFrames(node, tracks))
                {
                    if (BoxAt(node, defaults, tracks, keyFrame) != first)
                    {
                        Errors.Add($"{PathOf(node)}: the state has no duration, so its box keys must not change.");
                        break;
                    }
                }
                runs.Add((first, 0, int.MaxValue));
                return runs;
            }

            BoxFrame current = BoxAt(node, defaults, tracks, 0);
            int start = 0;
            for (int frame = 1; frame < duration; frame++)
            {
                BoxFrame box = BoxAt(node, defaults, tracks, frame);
                if (box != current)
                {
                    runs.Add((current, start, frame - 1));
                    current = box;
                    start = frame;
                }
            }
            runs.Add((current, start, duration - 1));
            return runs;
        }

        private BoxFrame BoxAt(SnappedNode2D node, BoxFrame defaults, BoxTracks tracks, int frame)
        {
            bool active = tracks.Get(node, "Active") is { } activeKeys && activeKeys.TryGetValueAt(frame, out Variant a) ? a.AsBool() : defaults.Active;
            Vector2I size = tracks.Get(node, "Size") is { } sizeKeys && sizeKeys.TryGetValueAt(frame, out Variant s) ? s.AsVector2I() : defaults.Size;
            Vector2I position = defaults.Position;
            if (tracks.Get(node, PositionProperty) is { } positionKeys && positionKeys.TryGetValueAt(frame, out Variant p))
            {
                // The key holds the local position (already checked: whole pixels). The chain offset does not change.
                position = position - ToVector2I(node.Position) + ToVector2I(p.AsVector2());
            }
            return new BoxFrame(active, position, size);
        }

        private static IEnumerable<int> KeyFrames(SnappedNode2D node, BoxTracks tracks)
        {
            if (!tracks.Nodes.TryGetValue(node, out Dictionary<string, PropertyKeys>? properties))
            {
                yield break;
            }
            foreach (PropertyKeys keys in properties.Values)
            {
                foreach ((int frame, Variant _) in keys.Keys)
                {
                    yield return frame;
                }
            }
        }

        /// <summary>The value of a box before its first key: the "RESET" animation key if there is one, else the scene value.</summary>
        private BoxFrame DefaultBox(SnappedNode2D node, BoxFrame sceneBox)
        {
            if (_player == null || _trackRoot == null || !_player.HasAnimation(ResetAnimation))
            {
                return sceneBox;
            }
            Animation reset = _player.GetAnimation(ResetAnimation);
            bool active = sceneBox.Active;
            Vector2I size = sceneBox.Size;
            Vector2I position = sceneBox.Position;
            for (int track = 0; track < reset.GetTrackCount(); track++)
            {
                if (reset.TrackGetType(track) != Animation.TrackType.Value || reset.TrackGetKeyCount(track) == 0)
                {
                    continue;
                }
                NodePath path = reset.TrackGetPath(track);
                if (_trackRoot.GetNodeOrNull(new NodePath(path.GetConcatenatedNames())) != node)
                {
                    continue;
                }
                Variant value = reset.TrackGetKeyValue(track, 0);
                switch (path.GetConcatenatedSubNames())
                {
                    case "Active":
                        active = value.AsBool();
                        break;
                    case "Size":
                        size = value.AsVector2I();
                        break;
                    case PositionProperty when IsWhole(value.AsVector2()):
                        position = position - ToVector2I(node.Position) + ToVector2I(value.AsVector2());
                        break;
                }
            }
            return new BoxFrame(active, position, size);
        }

        /// <summary>
        /// Reads the box tracks of the state animation (if there is one) and checks them. Tracks of other nodes (for
        /// example the bones of the visuals) are not read.
        /// </summary>
        private BoxTracks ReadBoxTracks(FighterState state, int duration)
        {
            var result = new BoxTracks();
            string name = state.Name;
            if (_player == null || _trackRoot == null || !_player.HasAnimation(name))
            {
                return result;
            }

            Animation animation = _player.GetAnimation(name);
            string where = $"Animation '{name}'";
            if (duration > 0)
            {
                double frames = animation.Length * GameConstants.TickRate;
                if (Math.Abs(frames - duration) > IntegerTolerance)
                {
                    Errors.Add($"{where}: the length is {frames:0.##} frames, but the state duration is {duration} frames.");
                }
            }

            for (int track = 0; track < animation.GetTrackCount(); track++)
            {
                NodePath path = animation.TrackGetPath(track);
                Node? node = _trackRoot.GetNodeOrNull(new NodePath(path.GetConcatenatedNames()));
                if (node == null)
                {
                    Errors.Add($"{where}: the track '{path}' points to a node that does not exist.");
                    continue;
                }
                if (node is not SnappedNode2D authoringNode)
                {
                    continue;   // Visuals (bones, sprites): presentation only.
                }
                if (node is not (FighterHitbox or FighterHurtbox))
                {
                    Errors.Add($"{where}: the track '{path}' animates an authoring node. Only hitboxes and hurtboxes can be keyed.");
                    continue;
                }
                if (node is FighterHitbox && node.GetParent() != state)
                {
                    Errors.Add($"{where}: the track '{path}' keys a hitbox of another state.");
                    continue;
                }

                string property = path.GetConcatenatedSubNames();
                if (animation.TrackGetType(track) != Animation.TrackType.Value || property is not ("Active" or "Size" or PositionProperty))
                {
                    Errors.Add($"{where}: the track '{path}' is not valid. Box tracks can key only position, Size, and Active.");
                    continue;
                }
                if (animation.ValueTrackGetUpdateMode(track) != Animation.UpdateMode.Discrete)
                {
                    Errors.Add($"{where}: the track '{path}' must use the Discrete update mode (no interpolation between frames).");
                    continue;
                }

                var keys = new PropertyKeys();
                for (int key = 0; key < animation.TrackGetKeyCount(track); key++)
                {
                    double time = animation.TrackGetKeyTime(track, key) * GameConstants.TickRate;
                    int frame = (int)Math.Round(time);
                    Variant value = animation.TrackGetKeyValue(track, key);
                    if (Math.Abs(time - frame) > IntegerTolerance)
                    {
                        Errors.Add($"{where}: the track '{path}' has a key at frame {time:0.###}. Keys must be on whole frames.");
                    }
                    else if (duration > 0 && frame >= duration)
                    {
                        Errors.Add($"{where}: the track '{path}' has a key at frame {frame}, after the end of the state ({duration} frames).");
                    }
                    else if (property == PositionProperty && !IsWhole(value.AsVector2()))
                    {
                        Errors.Add($"{where}: the track '{path}' has a position key {value.AsVector2()} that is not in whole pixels.");
                    }
                    else if (property == "Size" && (value.AsVector2I().X < 1 || value.AsVector2I().Y < 1))
                    {
                        Errors.Add($"{where}: the track '{path}' has a size key smaller than 1 x 1.");
                    }
                    else
                    {
                        keys.Keys.Add((frame, value));
                    }
                }
                keys.Keys.Sort((a, b) => a.Frame.CompareTo(b.Frame));

                if (!result.Nodes.TryGetValue(authoringNode, out Dictionary<string, PropertyKeys>? properties))
                {
                    properties = new Dictionary<string, PropertyKeys>();
                    result.Nodes[authoringNode] = properties;
                }
                properties[property] = keys;
            }
            return result;
        }

        private FrameActionData ConvertFrameAction(FighterFrameAction action, int duration, string where)
        {
            if (action.Frame < 0 || (duration > 0 && action.Frame >= duration))
            {
                Errors.Add($"{where}: the frame action '{action.Name}' is on frame {action.Frame}, outside the state.");
            }
            return new FrameActionData
            {
                Frame = action.Frame,
                Type = action.Type,
                Value = new FixedVector2(
                    ToFixed(action.Value.X, $"{PathOf(action)} Value.X"),
                    ToFixed(action.Value.Y, $"{PathOf(action)} Value.Y")),
            };
        }

        private List<TransitionData> ConvertTransitions(Node parent)
        {
            var result = new List<TransitionData>();
            foreach (Node child in parent.GetChildren())
            {
                if (child is not FighterTransition transition)
                {
                    continue;
                }
                string where = PathOf(transition);
                if (transition.Target.Length == 0)
                {
                    Errors.Add($"{where}: select the target state.");
                }

                var conditions = new List<ConditionData>();
                for (int i = 0; i < transition.Conditions.Count; i++)
                {
                    FighterCondition? condition = transition.Conditions[i];
                    if (condition == null)
                    {
                        Errors.Add($"{where}: condition {i} is empty.");
                        continue;
                    }
                    StateCondition? custom = null;
                    if (condition.Type == ConditionType.Custom && !_registry.TryGetCondition(condition.Custom, out custom))
                    {
                        Errors.Add($"{where}: condition {i}: there is no custom condition named '{condition.Custom}' in the hook registry.");
                    }
                    conditions.Add(new ConditionData
                    {
                        Type = condition.Type,
                        Buttons = IsInputCondition(condition.Type) ? condition.Buttons : InputFlags.None,
                        Direction = condition.Type == ConditionType.Direction ? condition.Direction : default,
                        Custom = custom,
                    });
                }

                result.Add(new TransitionData
                {
                    TargetState = ResolveState(transition.Target, where),
                    Conditions = conditions.ToArray(),
                    FromFrame = Math.Max(0, transition.FromFrame),
                    ToFrame = transition.ToFrame < 0 ? int.MaxValue : transition.ToFrame,
                });
            }
            return result;
        }

        private static bool IsInputCondition(ConditionType type) =>
            type is ConditionType.InputPressed or ConditionType.InputHeld or ConditionType.InputReleased;

        /// <summary>Checks that each authoring node has a valid parent.</summary>
        private void CheckPlacement()
        {
            foreach (FighterHitbox hitbox in FighterRoot.FindAll<FighterHitbox>(_root))
            {
                if (hitbox.GetParent() is not FighterState)
                {
                    Errors.Add($"{PathOf(hitbox)}: a hitbox must be a direct child of a FighterState.");
                }
            }
            foreach (FighterTransition transition in FighterRoot.FindAll<FighterTransition>(_root))
            {
                if (transition.GetParent() is not (FighterState or FighterSharedTransitions))
                {
                    Errors.Add($"{PathOf(transition)}: a transition must be a direct child of a FighterState or a FighterSharedTransitions node.");
                }
            }
            foreach (FighterFrameAction action in FighterRoot.FindAll<FighterFrameAction>(_root))
            {
                if (action.GetParent() is not FighterState)
                {
                    Errors.Add($"{PathOf(action)}: a frame action must be a direct child of a FighterState.");
                }
            }
            foreach (FighterHurtbox hurtbox in FighterRoot.FindAll<FighterHurtbox>(_root))
            {
                if (!DefaultHurtboxNodes().Contains(hurtbox))
                {
                    Errors.Add($"{PathOf(hurtbox)}: hurtboxes are body parts: put them outside the states. A state animation can move, resize, or turn them off.");
                }
            }
            foreach (FighterState state in FighterRoot.FindAll<FighterState>(_root))
            {
                for (Node? node = state.GetParent(); node != null && node != _root; node = node.GetParent())
                {
                    if (node is FighterState)
                    {
                        Errors.Add($"{PathOf(state)}: a state must not be inside another state.");
                        break;
                    }
                }
            }
        }

        /// <summary>The hurtboxes outside the states (body parts). A hurtbox inside a state is an error.</summary>
        private List<FighterHurtbox> DefaultHurtboxNodes()
        {
            var result = new List<FighterHurtbox>();
            foreach (FighterHurtbox hurtbox in FighterRoot.FindAll<FighterHurtbox>(_root))
            {
                bool inState = false;
                for (Node? node = hurtbox.GetParent(); node != null && node != _root; node = node.GetParent())
                {
                    inState |= node is FighterState;
                }
                if (!inState)
                {
                    result.Add(hurtbox);
                }
            }
            return result;
        }

        /// <summary>The scene value of a box, relative to the feet. False (and an error) if a value is not valid.</summary>
        private bool TryGetSceneBox(SnappedNode2D node, Vector2I size, bool active, out BoxFrame box)
        {
            box = default;
            if (!TryGetOffset(node, out Vector2I offset))
            {
                return false;
            }
            box = new BoxFrame(active, offset, size);
            return true;
        }

        /// <summary>The position of a node relative to the root: the sum of the local positions, in whole pixels.</summary>
        private bool TryGetOffset(Node2D node, out Vector2I offset)
        {
            offset = Vector2I.Zero;
            bool valid = true;
            for (Node? current = node; current != _root; current = current.GetParent())
            {
                if (current is not Node2D node2D)
                {
                    Errors.Add($"{PathOf(node)}: '{current?.Name}' is not a Node2D, so the position of the node is not defined.");
                    return false;
                }
                if (!IsWhole(node2D.Position) || node2D.Rotation != 0 || node2D.Scale != Vector2.One || node2D.Skew != 0)
                {
                    Errors.Add($"{PathOf(node2D)}: the position must be whole pixels, with no rotation, scale, or skew.");
                    valid = false;
                }
                offset += ToVector2I(node2D.Position);
            }
            return valid;
        }

        /// <summary>Checks the transform chain of a node with no box (a state): its children are positioned relative to it.</summary>
        private void CheckChain(Node2D node) => TryGetOffset(node, out _);

        private ushort ResolveState(string name, string where)
        {
            if (name.Length == 0)
            {
                return FighterStateData.NoState;
            }
            if (_stateIds.TryGetValue(name, out ushort id))
            {
                return id;
            }
            Errors.Add($"{where}: there is no state named '{name}'.");
            return FighterStateData.NoState;
        }

        private StateHook? ResolveHook(string name, string where)
        {
            if (name.Length == 0)
            {
                return null;
            }
            if (_registry.TryGetHook(name, out StateHook hook))
            {
                return hook;
            }
            Errors.Add($"{where}: there is no hook named '{name}' in the hook registry.");
            return null;
        }

        private Fixed ToFixed(float value, string where)
        {
            if (TryToFixed(value, out Fixed result))
            {
                return result;
            }
            Errors.Add($"{where}: the value {value} is not a valid number for the simulation.");
            return Fixed.Zero;
        }

        private AnimationPlayer? FindAnimationPlayer()
        {
            foreach (Node child in _root.GetChildren())
            {
                if (child is AnimationPlayer player)
                {
                    return player;
                }
            }
            return null;
        }

        private string PathOf(Node node) => _root.GetPathTo(node).ToString();

        private static bool IsWhole(Vector2 value) =>
            Math.Abs(value.X - Math.Round(value.X)) < IntegerTolerance && Math.Abs(value.Y - Math.Round(value.Y)) < IntegerTolerance;

        private static Vector2I ToVector2I(Vector2 value) => new((int)Math.Round(value.X), (int)Math.Round(value.Y));

        private static FixedAABB ToAabb(BoxFrame box) => new(
            new FixedVector2(Fixed.FromInt(box.Position.X), Fixed.FromInt(box.Position.Y)),
            new FixedVector2(Fixed.FromInt(box.Position.X + box.Size.X), Fixed.FromInt(box.Position.Y + box.Size.Y)));
    }
}

public sealed class FighterConversionException : Exception
{
    public IReadOnlyList<string> Errors { get; }

    public FighterConversionException(string scenePath, IReadOnlyList<string> errors)
        : base($"The fighter '{scenePath}' is not valid:\n  " + string.Join("\n  ", errors))
    {
        Errors = errors;
    }
}
