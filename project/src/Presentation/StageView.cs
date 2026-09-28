using FightingGame.Core;
using FightingGame.Simulation;
using Godot;

namespace FightingGame.Presentation;

/// <summary>Debug overlay: draws the converted stage boxes (the data the simulation uses) as rectangles.</summary>
public partial class StageView : Node2D
{
    [Export] public Color SolidColor { get; set; } = new(0.35f, 0.35f, 0.4f);
    [Export] public Color PlatformColor { get; set; } = new(0.55f, 0.45f, 0.3f);

    private StageData? _stage;

    public void SetStage(StageData stage)
    {
        _stage = stage;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_stage == null)
        {
            return;
        }

        foreach (FixedAABB solid in _stage.Solids)
        {
            DrawRect(solid.ToRect2(), SolidColor);
        }
        foreach (FixedAABB platform in _stage.Platforms)
        {
            DrawRect(platform.ToRect2(), PlatformColor);
        }
    }
}
