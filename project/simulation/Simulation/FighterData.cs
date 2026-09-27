using System.Runtime.CompilerServices;
using FightingGame.Core;

namespace FightingGame.Simulation;

public enum FighterAction : byte
{
    Idle,
    Walk,
    /// <summary>Short crouch on the ground before a ground jump.</summary>
    JumpSquat,
    Airborne,
    /// <summary>Short recovery after landing. The fighter cannot act.</summary>
    Land,
}

/// <summary>
/// The rollback state of one fighter. Plain value data only: it is copied for snapshots and hashed field by field.
/// Everything the simulation remembers about a fighter between frames must be in this struct.
/// </summary>
public struct FighterData
{
    /// <summary>True if this player slot is used in the match.</summary>
    public bool Active;

    /// <summary>Bottom-center of the collision box (the feet).</summary>
    public FixedVector2 Position;

    /// <summary>Pixels per frame.</summary>
    public FixedVector2 Velocity;

    /// <summary>-1 (left) or +1 (right).</summary>
    public sbyte Facing;

    public FighterAction Action;

    /// <summary>Frames since <see cref="Action"/> started. 0 on the first frame of the action.</summary>
    public int ActionFrame;

    /// <summary>True if the fighter stands on a solid or a platform after the last movement.</summary>
    public bool Grounded;

    /// <summary>While greater than 0, the fighter falls through one-way platforms.</summary>
    public int DropThroughTimer;

    /// <summary>Jumps available before the next landing (ground jump + air jumps).</summary>
    public byte JumpsLeft;

    /// <summary>The input of the previous frame. Used to detect buttons pressed on this frame.</summary>
    public InputFlags PrevInput;

    /// <summary>Changes the action. <see cref="ActionFrame"/> restarts only if the action is different.</summary>
    public void SetAction(FighterAction action)
    {
        if (Action != action)
        {
            Action = action;
            ActionFrame = 0;
        }
    }

    /// <summary>Adds every field. When you add a field to this struct, add it here too (a unit test checks this).</summary>
    public readonly void Hash(ref StateHasher hasher)
    {
        hasher.Add(Active);
        hasher.Add(Position);
        hasher.Add(Velocity);
        hasher.Add(Facing);
        hasher.Add((byte)Action);
        hasher.Add(ActionFrame);
        hasher.Add(Grounded);
        hasher.Add(DropThroughTimer);
        hasher.Add(JumpsLeft);
        hasher.Add((ushort)PrevInput);
    }
}

/// <summary>Fixed-size array of all fighters, stored inline (no heap allocation). Index = player slot.</summary>
[InlineArray(GameConstants.MaxPlayers)]
public struct FighterArray
{
    private FighterData _element0;
}
