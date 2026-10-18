using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FightingGame.Authoring;
using FightingGame.Presentation;
using FightingGame.Simulation;
using Godot;

namespace FightingGame.Debug;

/// <summary>
/// Debug tool: renders frames of fighter animations side by side into one PNG file, then quits. Each cell shows the
/// figure at one frame (the same pose code as the game, <see cref="FighterPose"/>), the frame label (a key frame has a
/// yellow label), and the active hurtboxes and hitboxes of that state frame from the converted fighter data.
/// </summary>
/// <remarks>
/// Arguments (after <c>--</c>):
/// <list type="bullet">
/// <item><c>--fighter=Fighter01</c>: a scene in res://scenes/fighters/, or a res:// path. Default Fighter01.</item>
/// <item><c>--anims=Idle,Walk</c>: one row (or more, when long) for each animation, with the frames that the game
/// shows: a state with a duration shows frames 0 to duration - 1; a looping animation, one loop; a non-looping one, also
/// its last frame (the pose that it holds). Default: all animations except RESET.</item>
/// <item><c>--sequence=Jump:10-21,Fall,Land:0-2</c>: the frames as they follow each other in the game, in one sequence
/// (to check that the end of one animation connects to the start of the next one). A name without a range = all frames.
/// Several sequences, separated by ";", start on new rows.</item>
/// <item><c>--out=/path/sheet.png</c>: the output file. Default: pose_sheet.png in a "fighting-game" folder in the
/// system temp folder (for example /tmp/fighting-game/pose_sheet.png).</item>
/// <item><c>--scale=2</c> (pixels per game pixel), <c>--columns=16</c> (cells per row), <c>--boxes=0</c> (no boxes).</item>
/// </list>
/// Rendering needs a window: the tool does not work with <c>--headless</c>.
/// </remarks>
public partial class PoseSheet : Node2D
{
    private const string FighterFolder = "res://scenes/fighters/";

    /// <summary>The size of one cell in game pixels, and the height of the ground line above the cell bottom.</summary>
    private static readonly Vector2 CellSize = new(70, 125);
    private const float GroundHeight = 18;

    private static readonly Color Background = new(0.16f, 0.16f, 0.18f);
    private static readonly Color CellBorder = new(1, 1, 1, 0.12f);
    private static readonly Color GroundColor = new(1, 1, 1, 0.3f);
    private static readonly Color LabelColor = new(0.75f, 0.75f, 0.75f);
    private static readonly Color KeyLabelColor = new(1f, 0.85f, 0.2f);
    private static readonly Color HurtboxColor = new(0.3f, 1f, 0.4f);
    private static readonly Color HitboxColor = new(1f, 0.2f, 0.2f);

    private readonly record struct Cell(int Column, int Row, string Animation, int Frame, bool IsKey);

    public override void _Ready()
    {
        try
        {
            Run();
        }
        catch (Exception exception)
        {
            GD.PrintErr($"PoseSheet: {exception.Message}");
            GetTree().Quit(1);
        }
    }

    private void Run()
    {
        Dictionary<string, string> args = ParseArguments(OS.GetCmdlineUserArgs());
        string fighterPath = args.TryGetValue("fighter", out string? fighter)
            ? (fighter.StartsWith("res://") ? fighter : $"{FighterFolder}{fighter}.tscn")
            : $"{FighterFolder}Fighter01.tscn";
        string output = args.TryGetValue("out", out string? outPath)
            ? outPath
            : System.IO.Path.Combine(System.IO.Path.GetTempPath(), "fighting-game", "pose_sheet.png");
        float scale = args.TryGetValue("scale", out string? scaleText) ? float.Parse(scaleText, CultureInfo.InvariantCulture) : 2;
        int columns = args.TryGetValue("columns", out string? columnsText) ? int.Parse(columnsText, CultureInfo.InvariantCulture) : 16;
        bool boxes = !args.TryGetValue("boxes", out string? boxesText) || boxesText != "0";

        var scene = GD.Load<PackedScene>(fighterPath) ?? throw new InvalidOperationException($"No fighter scene at '{fighterPath}'.");
        Node probe = scene.Instantiate();
        AnimationPlayer player = FighterPose.FindPlayer(probe) ?? throw new InvalidOperationException("The fighter has no AnimationPlayer.");
        // The fighter data gives the boxes, and the duration of each state (the frames that the game shows).
        FighterDefinitionData? definition = null;
        if (probe is FighterRoot root)
        {
            List<string> errors = FighterConverter.TryConvert(root, out definition);
            foreach (string error in errors)
            {
                GD.PrintErr($"PoseSheet: fighter error: {error}");
            }
        }

        List<Cell> cells = args.TryGetValue("sequence", out string? sequence)
            ? SequenceCells(player, definition, sequence, columns)
            : AnimationCells(player, definition, args.TryGetValue("anims", out string? anims) ? anims : null, columns);
        if (!boxes)
        {
            definition = null;
        }
        probe.Free();
        if (cells.Count == 0)
        {
            throw new InvalidOperationException("No frames to show.");
        }

        Vector2 cellPixels = CellSize * scale;
        int rows = cells.Max(c => c.Row) + 1;
        int usedColumns = cells.Max(c => c.Column) + 1;
        var viewport = new SubViewport
        {
            Size = new Vector2I((int)(usedColumns * cellPixels.X), (int)(rows * cellPixels.Y)),
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            TransparentBg = false,
        };
        AddChild(viewport);

        var background = new Node2D { Name = "Background" };
        background.Draw += () => DrawBackground(background, viewport.Size, cellPixels, usedColumns, rows);
        viewport.AddChild(background);

        foreach (Cell cell in cells)
        {
            var instance = scene.Instantiate<Node2D>();
            instance.Position = CellGround(cell, cellPixels, scale);
            instance.Scale = new Vector2(scale, scale);
            viewport.AddChild(instance);
            AnimationPlayer cellPlayer = FighterPose.FindPlayer(instance)!;
            FighterPose.UseManualProcess(cellPlayer);
            FighterPose.Show(cellPlayer, cell.Animation, cell.Frame);
        }

        var overlay = new Node2D { Name = "Overlay" };
        overlay.Draw += () => DrawOverlay(overlay, cells, cellPixels, scale, definition);
        viewport.AddChild(overlay);

        CaptureAndQuit(viewport, output);
    }

    /// <summary>One or more rows for each animation: all its frames (a non-looping animation also shows its last frame).</summary>
    private static List<Cell> AnimationCells(AnimationPlayer player, FighterDefinitionData? definition, string? names, int columns)
    {
        IEnumerable<string> animations = names != null
            ? names.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : player.GetAnimationList().Select(n => n.ToString()).Where(n => n != FighterPose.ResetAnimation);
        var cells = new List<Cell>();
        int row = 0;
        foreach (string name in animations)
        {
            Animation animation = GetAnimation(player, name);
            HashSet<int> keys = FighterPose.KeyFrames(animation);
            int last = LastFrame(animation, definition, name);
            for (int frame = 0; frame <= last; frame++)
            {
                if (frame > 0 && frame % columns == 0)
                {
                    row++;
                }
                cells.Add(new Cell(frame % columns, row, name, frame, keys.Contains(frame)));
            }
            row++;
        }
        return cells;
    }

    /// <summary>
    /// The frames of several animations as sequences ("Jump:10-21,Fall,Land:0-2"), wrapped into rows. Sequences separated
    /// by ";" start on a new row.
    /// </summary>
    private static List<Cell> SequenceCells(AnimationPlayer player, FighterDefinitionData? definition, string sequences, int columns)
    {
        var cells = new List<Cell>();
        int firstRow = 0;
        foreach (string sequence in sequences.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int start = cells.Count;
            AddSequence(player, definition, sequence, columns, firstRow, cells);
            if (cells.Count > start)
            {
                firstRow = cells[^1].Row + 1;
            }
        }
        return cells;
    }

    private static void AddSequence(AnimationPlayer player, FighterDefinitionData? definition, string sequence, int columns,
        int firstRow, List<Cell> cells)
    {
        int first = cells.Count;
        foreach (string part in sequence.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] nameAndRange = part.Split(':', 2);
            Animation animation = GetAnimation(player, nameAndRange[0]);
            HashSet<int> keys = FighterPose.KeyFrames(animation);
            int from = 0;
            int to = LastFrame(animation, definition, nameAndRange[0]);
            if (nameAndRange.Length == 2)
            {
                string[] range = nameAndRange[1].Split('-', 2);
                from = int.Parse(range[0], CultureInfo.InvariantCulture);
                to = range.Length == 2 ? int.Parse(range[1], CultureInfo.InvariantCulture) : from;
            }
            for (int frame = from; frame <= to; frame++)
            {
                int index = cells.Count - first;
                cells.Add(new Cell(index % columns, firstRow + index / columns, nameAndRange[0], frame, keys.Contains(frame)));
            }
        }
    }

    private static Animation GetAnimation(AnimationPlayer player, string name) =>
        player.HasAnimation(name) ? player.GetAnimation(name) : throw new InvalidOperationException($"No animation named '{name}'.");

    /// <summary>
    /// The last frame that the game shows: the last frame of the state duration; for a state with no duration, the
    /// last frame of a looping animation (it repeats), or the frame that a non-looping animation holds.
    /// </summary>
    private static int LastFrame(Animation animation, FighterDefinitionData? definition, string stateName)
    {
        FighterStateData? state = definition?.States.FirstOrDefault(s => s.Name == stateName);
        if (state != null && state.Duration > 0)
        {
            return state.Duration - 1;
        }
        return animation.LoopMode != Animation.LoopModeEnum.None ? FighterPose.FrameCount(animation) - 1 : FighterPose.FrameCount(animation);
    }

    private static Vector2 CellGround(Cell cell, Vector2 cellPixels, float scale) =>
        new((cell.Column + 0.5f) * cellPixels.X, (cell.Row + 1) * cellPixels.Y - GroundHeight * scale);

    private async void CaptureAndQuit(SubViewport viewport, string output)
    {
        // A few frames, so the poses are applied and drawn before the capture.
        for (int i = 0; i < 3; i++)
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        }
        Image image = viewport.GetTexture().GetImage();
        string path = output.StartsWith("user://") || output.StartsWith("res://") ? ProjectSettings.GlobalizePath(output) : output;
        string? folder = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path));
        if (folder != null)
        {
            System.IO.Directory.CreateDirectory(folder);
        }
        Error error = image.SavePng(path);
        if (error != Error.Ok)
        {
            GD.PrintErr($"PoseSheet: could not save '{path}': {error}");
            GetTree().Quit(1);
            return;
        }
        GD.Print($"PoseSheet: saved {path} ({image.GetWidth()} x {image.GetHeight()})");
        GetTree().Quit();
    }

    private static Dictionary<string, string> ParseArguments(string[] arguments)
    {
        var result = new Dictionary<string, string>();
        foreach (string argument in arguments)
        {
            string[] parts = argument.TrimStart('-').Split('=', 2);
            result[parts[0].ToLowerInvariant()] = parts.Length == 2 ? parts[1] : "";
        }
        return result;
    }

    /// <summary>The background, the cell borders, and the ground lines (drawn behind the figures).</summary>
    private static void DrawBackground(Node2D node, Vector2I size, Vector2 cellPixels, int columns, int rows)
    {
        node.DrawRect(new Rect2(Vector2.Zero, size), Background);
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                var rect = new Rect2(column * cellPixels.X, row * cellPixels.Y, cellPixels.X, cellPixels.Y);
                node.DrawRect(rect, CellBorder, filled: false, width: 1);
                float groundY = rect.End.Y - GroundHeight * (cellPixels.Y / CellSize.Y);
                node.DrawLine(new Vector2(rect.Position.X + 4, groundY), new Vector2(rect.End.X - 4, groundY), GroundColor, 1);
            }
        }
    }

    /// <summary>The labels and the active boxes of each cell (drawn on top of the figures).</summary>
    private static void DrawOverlay(Node2D node, List<Cell> cells, Vector2 cellPixels, float scale, FighterDefinitionData? definition)
    {
        Font font = ThemeDB.FallbackFont;
        foreach (Cell cell in cells)
        {
            var corner = new Vector2(cell.Column * cellPixels.X, cell.Row * cellPixels.Y);
            node.DrawString(font, corner + new Vector2(4, 14), $"{cell.Animation} {cell.Frame}", HorizontalAlignment.Left,
                cellPixels.X - 8, 12, cell.IsKey ? KeyLabelColor : LabelColor);

            FighterStateData? state = definition?.States.FirstOrDefault(s => s.Name == cell.Animation);
            if (definition == null || state == null)
            {
                continue;
            }
            Vector2 ground = CellGround(cell, cellPixels, scale);
            foreach (HurtboxData hurtbox in definition.HurtboxesOf(state))
            {
                if (hurtbox.IsActive(cell.Frame))
                {
                    node.DrawRect(ToRect(hurtbox.Box, ground, scale), HurtboxColor, filled: false, width: 1);
                }
            }
            foreach (HitboxData hitbox in state.Hitboxes)
            {
                if (hitbox.IsActive(cell.Frame))
                {
                    Rect2 rect = ToRect(hitbox.Box, ground, scale);
                    node.DrawRect(rect, HitboxColor with { A = 0.35f });
                    node.DrawRect(rect, HitboxColor, filled: false, width: 1);
                }
            }
        }
    }

    private static Rect2 ToRect(Core.FixedAABB box, Vector2 ground, float scale)
    {
        Rect2 rect = box.ToRect2();
        return new Rect2(ground + rect.Position * scale, rect.Size * scale);
    }
}
