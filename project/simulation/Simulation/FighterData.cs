using System.Runtime.CompilerServices;
using FightingGame.Core;

namespace FightingGame.Simulation;

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

    /// <summary>The current state: an index into <see cref="FighterDefinitionData.States"/>.</summary>
    public ushort StateId;

    /// <summary>Frames since the current state started. 0 on the first frame of the state.</summary>
    public int StateFrame;

    /// <summary>
    /// General values for state hooks (for example the charge time of a charged attack).
    /// They reset to 0 when a state starts.
    /// </summary>
    public int StateVar0;
    public int StateVar1;
    public int StateVar2;
    public int StateVar3;

    /// <summary>True if the fighter stands on a solid or a platform after the last movement.</summary>
    public bool Grounded;

    /// <summary>While greater than 0, the fighter falls through one-way platforms.</summary>
    public int DropThroughTimer;

    /// <summary>Jumps available before the next landing (ground jump + air jumps).</summary>
    public byte JumpsLeft;

    /// <summary>The input of the previous frame. Used to detect buttons pressed on this frame.</summary>
    public InputFlags PrevInput;

    /// <summary>Adds every field. When you add a field to this struct, add it here too (a unit test checks this).</summary>
    public readonly void Hash(ref StateHasher hasher)
    {
        hasher.Add(Active);
        hasher.Add(Position);
        hasher.Add(Velocity);
        hasher.Add(Facing);
        hasher.Add(StateId);
        hasher.Add(StateFrame);
        hasher.Add(StateVar0);
        hasher.Add(StateVar1);
        hasher.Add(StateVar2);
        hasher.Add(StateVar3);
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
