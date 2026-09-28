using System.Collections.Generic;
using System.Linq;
using Godot;

namespace FightingGame.Authoring;

/// <summary>
/// Groups two opposite <see cref="SpawnPosition"/> nodes. It must have exactly two <see cref="SpawnPosition"/> children.
/// A match with 2 or more players uses random pairs, so the players start at opposite places.
/// The editor draws a dashed line between the centers of the two fighter boxes.
/// </summary>
[Tool]
[GlobalClass]
public partial class SpawnPositionPair : StageNode
{
    public static readonly Color PairColor = new(0.3f, 0.8f, 1f);

    public override void _Notification(int what)
    {
        base._Notification(what);
        if (what == NotificationChildOrderChanged)
        {
            QueueRedraw();
            UpdateConfigurationWarnings();
            NotifyStageChanged();
        }
    }

    public List<SpawnPosition> GetSpawnPositions() => GetChildren().OfType<SpawnPosition>().ToList();

    public override string[] _GetConfigurationWarnings()
    {
        int count = GetSpawnPositions().Count;
        return count == 2
            ? System.Array.Empty<string>()
            : new[] { $"A SpawnPositionPair needs exactly 2 SpawnPosition children, but it has {count}." };
    }

    public override void _Draw()
    {
        if (!Engine.IsEditorHint())
        {
            return;
        }

        List<SpawnPosition> positions = GetSpawnPositions();
        if (positions.Count == 2)
        {
            // Connect the centers of the two fighter boxes, so the line does not lie on the floor edge.
            var center = new Vector2(0, -positions[0].FighterSize.Y / 2);
            DrawDashedLine(positions[0].Position + center, positions[1].Position + center, PairColor, 2, 10);
        }
    }
}
