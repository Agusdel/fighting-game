using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using FightingGame.App;
using FightingGame.Authoring;
using FightingGame.Core;
using FightingGame.PlayerInput;
using FightingGame.Session;
using FightingGame.Simulation;
using Godot;

namespace FightingGame.Presentation;

/// <summary>
/// Runs a match: it gives the local inputs to the match session at a fixed rate, and updates the views from the
/// session's world state. The session decides how the simulation runs (all players local for now).
/// </summary>
/// <remarks>
/// The simulation runs at <see cref="GameConstants.TickRate"/> ticks per second, independent of the render frame rate.
/// The accumulator uses a double. This is safe for determinism: it only decides when a tick runs,
/// never what a tick computes. The input of each tick is a plain <see cref="InputFlags"/> value.
///
/// The app gives the match a <see cref="MatchSetup"/> with <see cref="Setup"/> before the match enters the tree.
/// When the scene runs alone (F6 in the editor), the match builds a default setup from its exports.
/// </remarks>
public partial class MatchRunner : Node2D
{
    private const double TickDuration = 1.0 / GameConstants.TickRate;

    /// <summary>If rendering stops for a long time, run at most this many ticks in one frame, and drop the rest of the time.</summary>
    private const int MaxTicksPerFrame = 5;

    private static readonly StringName PauseAction = "match_pause";

    // Default setup, used only when the scene runs alone (no Setup call).

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

    /// <summary>Only for the default setup: the devices that this match owns (and disposes).</summary>
    private InputDevices? _ownInputDevices;

    private MatchSetup? _setup;

    /// <summary>Null until the stage loads. Without a valid stage, the match does not run.</summary>
    private IMatchSession? _session;
    private FighterView[] _fighterViews = System.Array.Empty<FighterView>();
    private double _accumulator;

    /// <summary>Raised when a player asks to leave the match (the pause action: Esc or the controller Start button).</summary>
    public event Action? ExitRequested;

    /// <summary>Sets the players, seed, stage, and rules. Call it before the match enters the tree.</summary>
    public void Setup(MatchSetup setup)
    {
        if (IsInsideTree())
        {
            throw new InvalidOperationException("MatchRunner.Setup must be called before the match enters the tree.");
        }
        _setup = setup;
    }

    public override void _Ready()
    {
        _setup ??= CreateDefaultSetup();

        StageData? stage = LoadStage(_setup.StageScene);
        if (stage == null)
        {
            SetProcess(false);
            return;
        }

        var data = new GameData
        {
            Stage = stage,
            FighterDefinition = DefaultGameData.CreateFighterDefinition(),
            Rules = _setup.Rules,
        };
        _session = new LocalSession(data, _setup.PlayerCount, _setup.Seed);

        if (StageView != null)
        {
            StageView.SetStage(data.Stage);
            StageView.Visible = ShowStageDebug;
        }

        Node2D fightersRoot = FightersRoot ?? this;
        _fighterViews = new FighterView[GameConstants.MaxPlayers];
        for (int i = 0; i < GameConstants.MaxPlayers; i++)
        {
            var view = new FighterView { Name = $"Fighter{i}", BodyColor = PlayerColors.Of(i) };
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
        _ownInputDevices?.Dispose();
        _ownInputDevices = null;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(PauseAction))
        {
            GetViewport().SetInputAsHandled();
            if (ExitRequested == null)
            {
                GD.Print("MatchRunner: the pause action has no effect when the match scene runs alone.");
            }
            ExitRequested?.Invoke();
            return;
        }

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
        if (_session == null)
        {
            return;
        }

        _accumulator += delta;

        int ticks = 0;
        while (_accumulator >= TickDuration && ticks < MaxTicksPerFrame)
        {
            _accumulator -= TickDuration;
            ticks++;
            for (int slot = 0; slot < _setup!.PlayerCount; slot++)
            {
                _session.SetLocalInput(slot, _setup.SlotDevices[slot].Read());
            }
            _session.AdvanceFrame();
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
    private static string BuildDebugText(in WorldData world, FighterDefinitionData definition)
    {
        var text = new StringBuilder();
        text.AppendLine($"Frame {world.Frame,8}   Hash {world.ComputeHash():X16}");
        text.AppendLine($"Round {world.Round,8}   {world.Phase} ({world.PhaseTimer})");
        for (int i = 0; i < GameConstants.MaxPlayers; i++)
        {
            ref readonly FighterData f = ref world.Fighters[i];
            if (!f.Active)
            {
                continue;
            }

            string state = definition.States[f.StateId].Name;
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
    /// The setup when the scene runs alone: the exports (stage, player count, seed), and for each slot the next
    /// device of keyboard 1, keyboard 2, then the connected controllers. Slots without a device are not created.
    /// </summary>
    private MatchSetup CreateDefaultSetup()
    {
        if (StageScene == null)
        {
            throw new InvalidOperationException("MatchRunner: StageScene is not set.");
        }

        _ownInputDevices = new InputDevices();
        IReadOnlyList<InputDevice> devices = _ownInputDevices.All;
        var slotDevices = new List<InputDevice>();
        for (int i = 0; i < PlayerCount && i < devices.Count; i++)
        {
            slotDevices.Add(devices[i]);
        }

        return new MatchSetup { SlotDevices = slotDevices, Seed = Seed, StageScene = StageScene };
    }

    /// <summary>
    /// Adds the stage scene as the first child (so it draws behind the fighters) and converts it to stage data.
    /// Returns null and logs all errors if the stage is not valid.
    /// </summary>
    private StageData? LoadStage(PackedScene stageScene)
    {
        if (stageScene.Instantiate() is not StageRoot stageRoot)
        {
            GD.PushError($"MatchRunner: the root of '{stageScene.ResourcePath}' is not a StageRoot.");
            return null;
        }

        // Stage positions are relative to the stage root. The root stays at the origin, so the art and the data match.
        stageRoot.Position = Vector2.Zero;
        AddChild(stageRoot);
        MoveChild(stageRoot, 0);

        try
        {
            StageData stage = StageConverter.Convert(stageRoot);
            GD.Print($"Stage '{stageScene.ResourcePath}' loaded. Stage hash: {stage.ComputeHash():X16}");
            return stage;
        }
        catch (StageConversionException exception)
        {
            GD.PushError(exception.Message);
            return null;
        }
    }


    private void RefreshViews()
    {
        if (_session == null)
        {
            return;
        }

        ref readonly WorldData world = ref _session.World;
        FighterDefinitionData definition = _session.Data.FighterDefinition;
        for (int i = 0; i < _fighterViews.Length; i++)
        {
            _fighterViews[i].Refresh(world.Fighters[i], definition, ShowCombatDebug);
        }

        Hud?.Refresh(world, definition, PlayerColors.All);

        if (DebugLabel != null)
        {
            DebugLabel.Text = BuildDebugText(world, definition);
        }
    }
}
