using Godot;

namespace FightingGame.Authoring;

/// <summary>
/// Base class for all stage authoring nodes. It snaps the transform (see <see cref="SnappedNode2D"/>), and asks the
/// <see cref="StageRoot"/> above it to check the stage again after each change.
/// Attach it directly to group nodes (for example "Structures" or "SpawnPositions").
/// </summary>
[Tool]
[GlobalClass]
public partial class StageNode : SnappedNode2D
{
    public override void _Notification(int what)
    {
        base._Notification(what);
        switch ((long)what)
        {
            case NotificationEnterTree:
            case NotificationExitTree:
            case NotificationLocalTransformChanged:
                NotifyStageChanged();
                break;
        }
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
}
