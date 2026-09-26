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

    [Export(PropertyHint.Range, "1,4")] public int PlayerCount { get; set; } = 1;
    [Export] public ulong Seed { get; set; } = 1;
    [Export] public StageView? StageView { get; set; }
    [Export] public Node2D? FightersRoot { get; set; }
    [Export] public Label? DebugLabel { get; set; }

    private readonly KeyboardInputMap[] _keyboardMaps = { KeyboardInputMap.Wasd, KeyboardInputMap.Arrows };

    private GameData _data = null!;
    private WorldState _state;
    private FighterView[] _fighterViews = System.Array.Empty<FighterView>();
    private double _accumulator;

    public override void _Ready()
    {
        _data = DefaultGameData.Create();
        _state = WorldState.Create(_data, PlayerCount, Seed);

        StageView?.SetStage(_data.Stage);

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

    public override void _Process(double delta)
    {
        _accumulator += delta;

        int ticks = 0;
        while (_accumulator >= TickDuration && ticks < MaxTicksPerFrame)
        {
            _accumulator -= TickDuration;
            ticks++;
            Simulator.Tick(ref _state, ReadInput(), _data);
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
            _fighterViews[i].Refresh(_state.Fighters[i], _data.Fighter);
        }

        if (DebugLabel != null)
        {
            ref readonly FighterState f = ref _state.Fighters[0];
            DebugLabel.Text =
                $"Frame {_state.Frame}   Hash {_state.ComputeHash():X16}\n" +
                $"P1 {f.Action} ({f.ActionFrame})  Pos {f.Position}  Vel {f.Velocity}\n" +
                $"Grounded {f.Grounded}  Jumps {f.JumpsLeft}  Drop {f.DropThroughTimer}";
        }
    }
}
