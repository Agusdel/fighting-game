using Godot;

namespace FightingGame.Authoring;

/// <summary>
/// Base class for all stage authoring nodes. It keeps the node transform exact for the fixed-point conversion.
/// Attach it directly to group nodes (for example "Structures" or "SpawnPositions").
/// </summary>
/// <remarks>
/// In the editor, each change of the local transform is corrected at once:
/// <list type="bullet">
/// <item><c>Position</c> is rounded to whole pixels. Godot stores positions as 32-bit floats, which hold every whole
/// number exactly, but most fractions not. The fixed-point conversion needs exact values.</item>
/// <item>Rotation, scale, and skew are reset. The simulation uses only axis-aligned boxes.</item>
/// </list>
/// This is only a help while editing. <see cref="StageConverter"/> checks all values again when the stage loads.
/// Tool scripts must not keep static state, so the editor can unload the C# assembly when it rebuilds.
/// </remarks>
[Tool]
[GlobalClass]
public partial class StageNode : Node2D
{
    public override void _Notification(int what)
    {
        switch ((long)what)
        {
            case NotificationEnterTree:
                SetNotifyLocalTransform(true);
                NotifyStageChanged();
                break;
            case NotificationExitTree:
                NotifyStageChanged();
                break;
            case NotificationLocalTransformChanged:
                if (Engine.IsEditorHint())
                {
                    SnapTransform();
                }
                OnLocalTransformChanged();
                NotifyStageChanged();
                break;
        }
    }

    /// <summary>Called after the local transform changes (after the snap in the editor).</summary>
    protected virtual void OnLocalTransformChanged()
    {
    }

    /// <summary>Asks the <see cref="StageRoot"/> above this node to check the stage again (the result shows as editor warnings).</summary>
    protected void NotifyStageChanged()
    {
        if (!Engine.IsEditorHint())
        {
            return;
        }

        for (Node? node = this; node != null; node = node.GetParent())
        {
            if (node is StageRoot root)
            {
                // Deferred: on exit, this node is still in the tree. The check must run after it leaves.
                root.CallDeferred(Node.MethodName.UpdateConfigurationWarnings);
                return;
            }
        }
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
