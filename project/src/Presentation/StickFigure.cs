using System.Collections.Generic;
using Godot;

namespace FightingGame.Presentation;

/// <summary>
/// Draws a stick figure from the <c>Bone2D</c> nodes of the <c>Skeleton2D</c> below it: one line for each bone, and a
/// filled circle at the tip of the "Neck" bone (the head). Presentation only: the gameplay boxes never depend on it.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Each bone points along its local +X axis: set the bone angle to 0, and the length explicitly. A bone with
/// length 0 (for example "Hip") draws no line.</item>
/// <item>Bones whose name ends with "Far" (the arm and leg away from the camera) are drawn first, in a darker color, so the
/// figure shows its facing without shoulders. Then the other bones, then the bones whose name ends with "Near", then
/// the head.</item>
/// <item>The figure is redrawn on each frame, because the animation changes the bones.</item>
/// </list>
/// </remarks>
[Tool]
[GlobalClass]
public partial class StickFigure : Node2D
{
    private const string HeadBone = "Neck";
    private const string FarSuffix = "Far";
    private const string NearSuffix = "Near";

    private readonly List<Bone2D> _bones = new();

    /// <summary>The color of the near limbs, the body, and the head (the player color in the game).</summary>
    [Export] public Color Color { get; set; } = Colors.White;

    [Export(PropertyHint.Range, "1,12,0.5,suffix:px")] public float LineWidth { get; set; } = 4;

    [Export(PropertyHint.Range, "1,20,0.5,suffix:px")] public float HeadRadius { get; set; } = 7;

    /// <summary>The brightness of the far limbs, as a fraction of <see cref="Color"/>.</summary>
    [Export(PropertyHint.Range, "0,1,0.05")] public float FarBrightness { get; set; } = 0.55f;

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        // Collected on each draw (a few nodes), so added, removed, or renamed bones in the editor show at once.
        CollectBones();
        Color far = new(Color.R * FarBrightness, Color.G * FarBrightness, Color.B * FarBrightness, Color.A);
        foreach (Bone2D bone in _bones)
        {
            if (bone.Name.ToString().EndsWith(FarSuffix))
            {
                DrawBone(bone, far);
            }
        }
        foreach (Bone2D bone in _bones)
        {
            string name = bone.Name.ToString();
            if (!name.EndsWith(FarSuffix) && !name.EndsWith(NearSuffix))
            {
                DrawBone(bone, Color);
            }
        }
        foreach (Bone2D bone in _bones)
        {
            if (bone.Name.ToString().EndsWith(NearSuffix))
            {
                DrawBone(bone, Color);
            }
        }
        foreach (Bone2D bone in _bones)
        {
            if (bone.Name == HeadBone)
            {
                DrawCircle(TipOf(bone), HeadRadius, Color);
            }
        }
    }

    private void DrawBone(Bone2D bone, Color color)
    {
        if (bone.GetLength() <= 0)
        {
            return;
        }
        DrawLine(ToLocal(bone.GlobalPosition), TipOf(bone), color, LineWidth, antialiased: true);
        // Round joints: the lines meet without gaps when a limb bends.
        DrawCircle(ToLocal(bone.GlobalPosition), LineWidth / 2, color);
        DrawCircle(TipOf(bone), LineWidth / 2, color);
    }

    private Vector2 TipOf(Bone2D bone) => ToLocal(bone.GlobalTransform * new Vector2(bone.GetLength(), 0));

    /// <summary>Finds all bones below this node, in tree order (so the draw order is stable).</summary>
    private void CollectBones()
    {
        _bones.Clear();
        Collect(this);
    }

    private void Collect(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is Bone2D bone)
            {
                _bones.Add(bone);
            }
            Collect(child);
        }
    }
}
