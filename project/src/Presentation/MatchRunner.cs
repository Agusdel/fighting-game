using System.Collections.Generic;
using System.Globalization;
using System.Text;
using FightingGame.Authoring;
using FightingGame.Core;
using FightingGame.PlayerInput;
using FightingGame.Simulation;
using Godot;

namespace FightingGame.Presentation;

/// <summary>
/// Runs a local match: it owns the world state, runs the simulation at a fixed rate, and updates the views.
/// </summary>
/// <remarks>
/// The simulation runs at <see cref="GameConstants.TickRate"/> ticks per second, independent of the render frame rate.
/// The accumulator uses a double. This is safe for determinism: it only decides when a tick runs,
/// never what a tick computes. The input of each tick is a plain <see cref="InputFlags"/> value.
/// </remarks>
public partial class MatchRunner : Node2D
{
    private const double TickDuration = 1.0 / GameConstants.TickRate;

    /// <summary>If rendering stops for a long time, run at most this many ticks in one frame, and drop the rest of the time.</summary>
    private const int MaxTicksPerFrame = 5;

    private static readonly Color[] PlayerColors =
    {
        new(0.9f, 0.3f, 0.3f),
        new(0.3f, 0.5f, 0.95f),
        new(0.3f, 0.85f, 0.4f),
        new(0.95f, 0.8f, 0.2f),
    };

    /// <summary>A stage scene. Its root must be a <see cref="StageRoot"/> (stage scenes inherit StageBase.tscn).</summary>
    [Export] public PackedScene? StageScene { get; set; }
    [Export(PropertyHint.Range, "1,4")] public int PlayerCount { get; set; } = 1;
    [Export] public ulong Seed { get; set; } = 1;

    /// <summary>Shows the collision boxes of the stage. F1 switches it on and off in the game.</summary>
    [Export] public bool ShowStageDebug { get; set; }

    /// <summary>Shows the active hurtboxes and hitboxes of the fighters. F2 switches it on and off in the game.</summary>
    [Export] public bool ShowCombatDebug { get; set; }

    [Export] public StageView? StageView { get; set; }
    [Export] public HudView? Hud { get; set; }
    [Export] public Node2D? FightersRoot { get; set; }
    [Export] public Label? DebugLabel { get; set; }

    private InputDevices? _inputDevices;

    /// <summary>The input device of each player slot. Slots without a device get no input.</summary>
    private InputDevice[] _slotDevices = System.Array.Empty<InputDevice>();

    private GameData _data = null!;
    private WorldData _world;
    private FighterView[] _fighterViews = System.Array.Empty<FighterView>();
    private double _accumulator;

    public override void _Ready()
    {
        // Default devices: keyboard 1, keyboard 2, then the connected controllers, in slot order.
        _inputDevices = new InputDevices();
        IReadOnlyList<InputDevice> devices = _inputDevices.All;
        _slotDevices = new InputDevice[Mathf.Min(PlayerCount, devices.Count)];
        for (int i = 0; i < _slotDevices.Length; i++)
        {
            _slotDevices[i] = devices[i];
        }

        StageData? stage = LoadStage();
        if (stage == null)
        {
            SetProcess(false);
            return;
        }

        _data = new GameData
        {
            Stage = stage,
            FighterDefinition = DefaultGameData.CreateFighterDefinition(),
        };
        _world = WorldData.Create(_data, PlayerCount, Seed);

        if (StageView != null)
        {
            StageView.SetStage(_data.Stage);
            StageView.Visible = ShowStageDebug;
        }

        Node2D fightersRoot = FightersRoot ?? this;
        _fighterViews = new FighterView[GameConstants.MaxPlayers];
        for (int i = 0; i < GameConstants.MaxPlayers; i++)
        {
            var view = new FighterView { Name = $"Fighter{i}", BodyColor = PlayerColors[i] };
            fightersRoot.AddChild(view);
            _fighterViews[i] = view;
        }

        if (DebugLabel != null)
        {
            // A monospace font keeps the fixed-width numbers of the debug text in columns.
            DebugLabel.AddThemeFontOverride("font", new SystemFont { FontNames = new[] { "monospace" } });
            DebugLabel.AddThemeFontSizeOverride("font_size", 13);
        }

        RefreshViews();
    }

    public override void _ExitTree()
    {
        _inputDevices?.Dispose();
        _inputDevices = null;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return;
        }

        if (key.PhysicalKeycode == Key.F1 && StageView != null)
        {
            ShowStageDebug = !ShowStageDebug;
            StageView.Visible = ShowStageDebug;
            GetViewport().SetInputAsHandled();
        }
        else if (key.PhysicalKeycode == Key.F2)
        {
            ShowCombatDebug = !ShowCombatDebug;
            RefreshViews();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        _accumulator += delta;

        int ticks = 0;
        while (_accumulator >= TickDuration && ticks < MaxTicksPerFrame)
        {
            _accumulator -= TickDuration;
            ticks++;
            Simulator.Tick(ref _world, ReadInput(), _data);
        }

        if (ticks == MaxTicksPerFrame)
        {
            _accumulator = 0;
        }

        if (ticks > 0)
        {
            RefreshViews();
        }
    }

    /// <summary>
    /// Debug text: one line for each value, numbers with a fixed width. With the monospace font of the label,
    /// the columns do not move when the values change.
    /// </summary>
    private string BuildDebugText()
    {
        var text = new StringBuilder();
        text.AppendLine($"Frame {_world.Frame,8}   Hash {_world.ComputeHash():X16}");
        text.AppendLine($"Round {_world.Round,8}   {_world.Phase} ({_world.PhaseTimer})");
        for (int i = 0; i < GameConstants.MaxPlayers; i++)
        {
            ref readonly FighterData f = ref _world.Fighters[i];
            if (!f.Active)
            {
                continue;
            }

            string state = _data.FighterDefinition.States[f.StateId].Name;
            text.AppendLine();
            text.AppendLine($"P{i + 1}  {state,-16} frame {f.StateFrame,5}");
            text.AppendLine($"    Position  {FormatVector(f.Position)}");
            text.AppendLine($"    Velocity  {FormatVector(f.Velocity)}");
            text.AppendLine($"    Health {f.Health,4}   Grounded {(f.Grounded ? "yes" : "no "),-3}   Jumps {f.JumpsLeft}   Hitstop {f.HitstopFrames,2}");
        }
        return text.ToString();
    }

    private static string FormatVector(FixedVector2 value) => $"({Format(value.X)}, {Format(value.Y)})";

    /// <summary>Two decimals, right-aligned to 8 characters. For display only.</summary>
    private static string Format(Fixed value) =>
        value.ToFloat().ToString("0.00", CultureInfo.InvariantCulture).PadLeft(8);

    /// <summary>
    /// Adds the stage scene as the first child (so it draws behind the fighters) and converts it to stage data.
    /// Returns null and logs all errors if the stage is not valid.
    /// </summary>
    private StageData? LoadStage()
    {
        if (StageScene == null)
        {
            GD.PushError("MatchRunner: StageScene is not set.");
            return null;
        }

        if (StageScene.Instantiate() is not StageRoot stageRoot)
        {
            GD.PushError($"MatchRunner: the root of '{StageScene.ResourcePath}' is not a StageRoot.");
            return null;
        }

        // Stage positions are relative to the stage root. The root stays at the origin, so the art and the data match.
        stageRoot.Position = Vector2.Zero;
        AddChild(stageRoot);
        MoveChild(stageRoot, 0);

        try
        {
            StageData stage = StageConverter.Convert(stageRoot);
            GD.Print($"Stage '{StageScene.ResourcePath}' loaded. Stage hash: {stage.ComputeHash():X16}");
            return stage;
        }
        catch (StageConversionException exception)
        {
            GD.PushError(exception.Message);
            return null;
        }
    }

    private FrameInput ReadInput()
    {
        FrameInput input = default;
        for (int i = 0; i < _slotDevices.Length; i++)
        {
            input[i] = _slotDevices[i].Read();
        }
        return input;
    }

    private void RefreshViews()
    {
        for (int i = 0; i < _fighterViews.Length; i++)
        {
            _fighterViews[i].Refresh(_world.Fighters[i], _data.FighterDefinition, ShowCombatDebug);
        }

        Hud?.Refresh(_world, _data.FighterDefinition, PlayerColors);

        if (DebugLabel != null)
        {
            DebugLabel.Text = BuildDebugText();
        }
    }
}
