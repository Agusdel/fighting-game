using Godot;

namespace FightingGame.Authoring;

/// <summary>
/// Base class for authoring nodes whose position becomes fixed-point data (stage and fighter nodes).
/// It keeps the node transform exact for the conversion. Attach it directly to group nodes, so the positions
/// from the node to the root are all exact.
/// </summary>
/// <remarks>
/// In the editor, each change of the local transform is corrected at once:
/// <list type="bullet">
/// <item><c>Position</c> is rounded to whole pixels. Godot stores positions as 32-bit floats, which hold every whole
/// number exactly, but most fractions not. The fixed-point conversion needs exact values.</item>
/// <item>Rotation, scale, and skew are reset. The simulation uses only axis-aligned boxes.</item>
/// </list>
/// This also works with the animation editor: a node snaps when it is dragged (before a key is inserted), and
/// discrete keys already have whole values. It is only a help while editing: the converters check all values again.
/// Tool scripts must not keep static state, so the editor can unload the C# assembly when it rebuilds.
/// </remarks>
[Tool]
[GlobalClass]
public partial class SnappedNode2D : Node2D
{
    public override void _Notification(int what)
    {
        switch ((long)what)
        {
            case NotificationEnterTree:
                SetNotifyLocalTransform(true);
                break;
            case NotificationLocalTransformChanged:
                if (Engine.IsEditorHint())
                {
                    SnapTransform();
                }
                OnLocalTransformChanged();
                break;
        }
    }

    /// <summary>Called after the local transform changes (after the snap in the editor).</summary>
    protected virtual void OnLocalTransformChanged()
    {
    }

    private void SnapTransform()
    {
        Vector2 snapped = Position.Round();
        // Only write when a value is wrong: each write sends a new transform notification.
        if (Position != snapped)
        {
            Position = snapped;
        }
        if (Rotation != 0)
        {
            Rotation = 0;
        }
        if (Scale != Vector2.One)
        {
            Scale = Vector2.One;
        }
        if (Skew != 0)
        {
            Skew = 0;
        }
    }
}
