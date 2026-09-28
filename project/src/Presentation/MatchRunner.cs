using FightingGame.Authoring;
using FightingGame.Core;
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

    [Export] public StageView? StageView { get; set; }
    [Export] public Node2D? FightersRoot { get; set; }
    [Export] public Label? DebugLabel { get; set; }

    private readonly KeyboardInputMap[] _keyboardMaps = { KeyboardInputMap.Wasd, KeyboardInputMap.Arrows };

    private GameData _data = null!;
    private WorldData _world;
    private FighterView[] _fighterViews = System.Array.Empty<FighterView>();
    private double _accumulator;

    public override void _Ready()
    {
        StageData? stage = LoadStage();
        if (stage == null)
        {
            SetProcess(false);
            return;
        }

        _data = new GameData
        {
            Stage = stage,
            Fighter = DefaultGameData.CreateFighterStats(),
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

        RefreshViews();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.F1 } && StageView != null)
        {
            ShowStageDebug = !ShowStageDebug;
            StageView.Visible = ShowStageDebug;
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
        for (int i = 0; i < PlayerCount && i < _keyboardMaps.Length; i++)
        {
            input[i] = _keyboardMaps[i].Read();
        }
        return input;
    }

    private void RefreshViews()
    {
        for (int i = 0; i < _fighterViews.Length; i++)
        {
            _fighterViews[i].Refresh(_world.Fighters[i], _data.Fighter);
        }

        if (DebugLabel != null)
        {
            ref readonly FighterData f = ref _world.Fighters[0];
            DebugLabel.Text =
                $"Frame {_world.Frame}   Hash {_world.ComputeHash():X16}\n" +
                $"P1 {f.Action} ({f.ActionFrame})  Pos {f.Position}  Vel {f.Velocity}\n" +
                $"Grounded {f.Grounded}  Jumps {f.JumpsLeft}  Drop {f.DropThroughTimer}";
        }
    }
}
