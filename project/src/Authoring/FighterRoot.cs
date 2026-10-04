using System.Collections.Generic;
using Godot;

namespace FightingGame.Authoring;

/// <summary>
/// Root node of a fighter scene. The origin is the feet of the fighter. The authoring nodes below it define the
/// complete fighter: <see cref="FighterCollisionBox"/>, <see cref="FighterHurtbox"/> body parts, <see cref="FighterState"/>
/// nodes with their hitboxes, transitions, and frame actions, and <see cref="FighterSharedTransitions"/>.
/// One animation in the <c>AnimationPlayer</c> for each state (with the state name) holds the visuals and the box keys.
/// </summary>
/// <remarks>
/// The values in pixels per frame can have fractions (for example a gravity of 0.6). The fixed-point conversion of
/// these values is exact and the same on every machine.
/// In the editor, only the hitboxes of the state whose animation is open in the animation editor are drawn.
/// </remarks>
[Tool]
[GlobalClass]
public partial class FighterRoot : SnappedNode2D
{
    private const float OriginMarkerSize = 8;

    private string _idleState = "Idle";
    private string _hitstunState = "";
    private string _deadState = "";

    /// <summary>The animation open in the editor ("" = none). Only the hitboxes of this state are drawn.</summary>
    private string _shownState = "";

    [ExportGroup("Movement")]
    [Export(PropertyHint.None, "suffix:px/frame")] public float WalkSpeed { get; set; } = 5;

    /// <summary>Maximum horizontal speed in the air.</summary>
    [Export(PropertyHint.None, "suffix:px/frame")] public float AirSpeed { get; set; } = 4.5f;

    /// <summary>Horizontal acceleration in the air while a direction is held.</summary>
    [Export(PropertyHint.None, "suffix:px/frame²")] public float AirAcceleration { get; set; } = 0.5f;

    /// <summary>Horizontal deceleration in the air while no direction is held.</summary>
    [Export(PropertyHint.None, "suffix:px/frame²")] public float AirFriction { get; set; } = 0.25f;

    /// <summary>Horizontal deceleration on the ground while the fighter has no control (knockback).</summary>
    [Export(PropertyHint.None, "suffix:px/frame²")] public float GroundFriction { get; set; } = 1;

    [Export(PropertyHint.None, "suffix:px/frame²")] public float Gravity { get; set; } = 0.6f;

    [Export(PropertyHint.None, "suffix:px/frame")] public float MaxFallSpeed { get; set; } = 12;

    /// <summary>Vertical velocity at the start of a jump. Negative, because Y points down.</summary>
    [Export(PropertyHint.None, "suffix:px/frame")] public float JumpVelocity { get; set; } = -13;

    /// <summary>Total jumps before landing again (1 ground jump + air jumps).</summary>
    [Export(PropertyHint.Range, "1,10,1")] public int MaxJumps { get; set; } = 2;

    /// <summary>Frames that the fighter ignores one-way platforms after a drop-through.</summary>
    [Export(PropertyHint.Range, "1,120,1,suffix:frames")] public int DropThroughFrames { get; set; } = 12;

    [ExportGroup("Combat")]
    [Export(PropertyHint.Range, "1,999,1,or_greater")] public int MaxHealth { get; set; } = 100;

    [ExportGroup("States")]
    /// <summary>The state of the fighter when it spawns.</summary>
    [Export] public string IdleState { get => _idleState; set => SetStateName(ref _idleState, value); }

    /// <summary>The state after a hit. Empty = hits do damage only (no hitstun, no knockback).</summary>
    [Export] public string HitstunState { get => _hitstunState; set => SetStateName(ref _hitstunState, value); }

    /// <summary>The state when the health reaches 0. Empty = no state change.</summary>
    [Export] public string DeadState { get => _deadState; set => SetStateName(ref _deadState, value); }

    /// <summary>The names of all <see cref="FighterState"/> nodes below the root, in tree order.</summary>
    public List<string> GetStateNames()
    {
        var names = new List<string>();
        foreach (FighterState state in FindAll<FighterState>(this))
        {
            names.Add(state.Name);
        }
        return names;
    }

    /// <summary>True if the hitboxes of the state are drawn in the editor (the state animation is open, or no state animation is open).</summary>
    public bool IsStateShown(string stateName) => _shownState.Length == 0 || _shownState == stateName;

    public override void _Process(double delta)
    {
        if (!Engine.IsEditorHint())
        {
            SetProcess(false);
            return;
        }

        string open = FindAnimationPlayer()?.AssignedAnimation.ToString() ?? "";
        string shown = GetStateNames().Contains(open) ? open : "";
        if (shown != _shownState)
        {
            _shownState = shown;
            foreach (FighterHitbox hitbox in FindAll<FighterHitbox>(this))
            {
                hitbox.QueueRedraw();
            }
        }
    }

    public override void _Draw()
    {
        if (!Engine.IsEditorHint())
        {
            return;
        }
        DrawLine(new Vector2(-OriginMarkerSize, 0), new Vector2(OriginMarkerSize, 0), Colors.White, 1);
        DrawLine(new Vector2(0, -OriginMarkerSize), new Vector2(0, OriginMarkerSize), Colors.White, 1);
    }

    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        string name = property["name"].AsString();
        if (name is nameof(IdleState) or nameof(HitstunState) or nameof(DeadState))
        {
            FighterAuthoring.SetNameSuggestions(property, GetStateNames());
        }
    }

    /// <summary>The conversion errors (the same check as when the game loads the fighter).</summary>
    public override string[] _GetConfigurationWarnings() => FighterConverter.TryConvert(this, out _).ToArray();

    /// <summary>All nodes of a type below <paramref name="node"/>, depth-first in child order.</summary>
    internal static List<T> FindAll<T>(Node node) where T : Node
    {
        var found = new List<T>();
        AddAll(node, found);
        return found;
    }

    private static void AddAll<T>(Node node, List<T> found) where T : Node
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is T match)
            {
                found.Add(match);
            }
            AddAll(child, found);
        }
    }

    private AnimationPlayer? FindAnimationPlayer()
    {
        foreach (Node child in GetChildren())
        {
            if (child is AnimationPlayer player)
            {
                return player;
            }
        }
        return null;
    }

    private void SetStateName(ref string field, string value)
    {
        field = value.Trim();
        UpdateConfigurationWarnings();
    }
}
