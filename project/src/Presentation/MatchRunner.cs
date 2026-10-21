using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using FightingGame.App;
using FightingGame.Networking;
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

    /// <summary>While the debug step key is held: the time before the steps repeat, and the time between steps.</summary>
    private const double DebugStepRepeatDelay = 0.4;
    private const double DebugStepRepeatInterval = 1.0 / 20;

    private static readonly StringName PauseAction = "match_pause";
    private static readonly StringName DebugPauseAction = "debug_pause";
    private static readonly StringName DebugStepAction = "debug_step";

    // Default setup, used only when the scene runs alone (no Setup call).

    /// <summary>A stage scene. Its root must be a <see cref="StageRoot"/> (stage scenes inherit StageBase.tscn).</summary>
    [Export] public PackedScene? StageScene { get; set; }

    /// <summary>A fighter scene. Its root must be a <see cref="FighterRoot"/> (fighter scenes inherit FighterBase.tscn).</summary>
    [Export] public PackedScene? FighterScene { get; set; }
    [Export(PropertyHint.Range, "1,4")] public int PlayerCount { get; set; } = 1;
    [Export] public ulong Seed { get; set; } = 1;
    [Export] public MatchSessionType SessionType { get; set; } = MatchSessionType.Local;

    [ExportSubgroup("Loopback session (debug)")]
    [Export(PropertyHint.Range, "0,500,1,suffix:ms")] public int LoopbackLatencyMs { get; set; } = 50;
    [Export(PropertyHint.Range, "0,200,1,suffix:ms")] public int LoopbackJitterMs { get; set; } = 10;
    [Export(PropertyHint.Range, "0,100,1,suffix:%")] public int LoopbackLossPercent { get; set; } = 2;
    [ExportGroup("")]

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

    /// <summary>
    /// Set when a debug session finds a desync (<see cref="SyncTestException"/>, <see cref="DesyncException"/>).
    /// The match stops and shows the report.
    /// </summary>
    private Exception? _desyncError;

    /// <summary>
    /// Debug pause: the simulation does not run, and the views keep the last state. Home pauses and resumes. End runs
    /// exactly one tick (with the inputs held at that moment) and pauses. See <see cref="CanUseDebugPause"/>.
    /// </summary>
    private bool _debugPaused;

    /// <summary>How long the debug step key is held, and the repeated steps that ran in that time.</summary>
    private double _debugStepHeldTime;
    private int _debugStepRepeats;

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
        FighterDefinitionData? fighter = LoadFighter(_setup.FighterScene);
        if (stage == null || fighter == null)
        {
            SetProcess(false);
            return;
        }

        var data = new GameData
        {
            Stage = stage,
            FighterDefinition = fighter,
            Rules = _setup.Rules,
        };
        _session = CreateSession(data, _setup);

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
            if (i < _setup.PlayerCount)
            {
                view.SetVisual(_setup.FighterScene);
            }
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

        HandleDebugInput(@event);
    }

    /// <summary>
    /// The debug keys: the debug pause (Home, End) and the debug views (F1 to F3). They work only in a debug build (the
    /// editor, or an export with a debug template); a release export ignores them.
    /// </summary>
    private void HandleDebugInput(InputEvent @event)
    {
        if (!OS.IsDebugBuild())
        {
            return;
        }

        if (@event.IsActionPressed(DebugPauseAction) && CanUseDebugPause())
        {
            GetViewport().SetInputAsHandled();
            _debugPaused = !_debugPaused;
            _accumulator = 0;
            RefreshViews();
            return;
        }

        // The key repeat of the system (echo events) is not used: it repeats only the key pressed last, so a jump press
        // stops it. RepeatHeldDebugStep repeats the steps while the key is held.
        if (@event.IsActionPressed(DebugStepAction) && CanUseDebugPause())
        {
            GetViewport().SetInputAsHandled();
            _debugPaused = true;
            _accumulator = 0;
            _debugStepHeldTime = 0;
            _debugStepRepeats = 0;
            RunTick();
            RefreshViews();
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
        else if (key.PhysicalKeycode == Key.F3 && _session is LoopbackMatchSession loopback)
        {
            loopback.ShowNextPeer();
            RefreshViews();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        if (_session == null || _desyncError != null)
        {
            return;
        }

        if (_debugPaused)
        {
            RepeatHeldDebugStep(delta);
            return;
        }

        _accumulator += delta;

        int ticks = 0;
        while (_accumulator >= TickDuration && ticks < MaxTicksPerFrame)
        {
            _accumulator -= TickDuration;
            ticks++;
            if (!RunTick())
            {
                return;
            }
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
    /// While the debug step key is held (after the first step of the key press), runs one tick each
    /// <see cref="DebugStepRepeatInterval"/>, after <see cref="DebugStepRepeatDelay"/>. It reads the held state of the
    /// action, so presses of other keys do not stop it.
    /// </summary>
    private void RepeatHeldDebugStep(double delta)
    {
        if (!Input.IsActionPressed(DebugStepAction) || !CanUseDebugPause())
        {
            return;
        }

        _debugStepHeldTime += delta;
        if (_debugStepHeldTime < DebugStepRepeatDelay)
        {
            return;
        }

        int due = 1 + (int)((_debugStepHeldTime - DebugStepRepeatDelay) / DebugStepRepeatInterval);
        int ticks = 0;
        while (_debugStepRepeats < due && ticks < MaxTicksPerFrame)
        {
            _debugStepRepeats++;
            ticks++;
            if (!RunTick())
            {
                break;
            }
        }
        // After a long frame, drop the steps that are late (the same as the normal run).
        _debugStepRepeats = Math.Max(_debugStepRepeats, due);

        if (ticks > 0)
        {
            RefreshViews();
        }
    }

    /// <summary>
    /// Gives the local inputs to the session and runs one tick. Returns false if the match is stopped (no session, or a
    /// desync was found in this tick or before).
    /// </summary>
    private bool RunTick()
    {
        if (_session == null || _desyncError != null)
        {
            return false;
        }

        for (int slot = 0; slot < _setup!.PlayerCount; slot++)
        {
            _session.SetLocalInput(slot, _setup.SlotDevices[slot].Read());
        }
        try
        {
            _session.AdvanceFrame();
        }
        catch (Exception exception) when (exception is SyncTestException or DesyncException)
        {
            StopWithDesyncError(exception);
            return false;
        }
        return true;
    }

    /// <summary>
    /// The debug pause keys work only in a debug build (the editor, or an export with a debug template; a release
    /// export ignores them) and only in a session where all players are on this machine. In an online match, a pause
    /// on one machine would make the other machines wait or desync.
    /// </summary>
    private bool CanUseDebugPause() =>
        OS.IsDebugBuild() && _session is (LocalSession or SyncTestSession or LoopbackMatchSession);

    /// <summary>
    /// Freezes the match (the views keep the last state) and shows the report on top of the debug text.
    /// The pause action still leaves the match.
    /// </summary>
    private void StopWithDesyncError(Exception exception)
    {
        _desyncError = exception;
        GD.PushError(exception.Message);
        RefreshViews();
    }

    /// <summary>The session of the setup. A loopback match with 1 player is not possible: it runs as a local match.</summary>
    private static IMatchSession CreateSession(GameData data, MatchSetup setup)
    {
        switch (setup.SessionType)
        {
            case MatchSessionType.SyncTest:
                return new SyncTestSession(data, setup.PlayerCount, setup.Seed);
            case MatchSessionType.Loopback when setup.PlayerCount >= 2:
                LoopbackSettings network = setup.Loopback;
                return new LoopbackMatchSession(data, setup.PlayerCount, setup.Seed, network.LatencyMs, network.JitterMs, network.LossPercent);
            case MatchSessionType.Loopback:
                GD.PushWarning("MatchRunner: a loopback match needs 2 or more players. The match runs as a local match.");
                return new LocalSession(data, setup.PlayerCount, setup.Seed);
            default:
                return new LocalSession(data, setup.PlayerCount, setup.Seed);
        }
    }

    /// <summary>The first lines of the debug text: how the match runs, and the network values of a loopback match.</summary>
    private string SessionDescription()
    {
        string description = _session switch
        {
            SyncTestSession syncTest => $"SyncTest (check distance {syncTest.CheckDistance})",
            LoopbackMatchSession loopback => DescribeLoopback(loopback),
            _ => "Local",
        };
        if (_desyncError != null)
        {
            return $"{description}\nDESYNC: the match is stopped";
        }
        return _debugPaused ? $"{description}   PAUSED (Home: resume, End: next frame)" : description;
    }

    private string DescribeLoopback(LoopbackMatchSession loopback)
    {
        RollbackSession shown = loopback.ShownSession;
        RollbackSessionStats stats = shown.Stats;
        LoopbackNetwork network = loopback.Network;
        string ahead = shown.FramesAhead.ToFloat().ToString("0.00", CultureInfo.InvariantCulture);
        return $"Loopback: P{loopback.ShownPeer + 1} view (F3: next)   {network.LatencyMs} ms + {network.JitterMs} ms jitter, {network.LossPercent} % loss\n"
            + $"Rollbacks {stats.Rollbacks,6} (longest {stats.LongestRollback})   Waits: prediction {stats.PredictionWaits}, time sync {stats.TimeSyncWaits}\n"
            + $"Confirmed {shown.ConfirmedFrame,6}   Ahead {ahead}   Hashes compared {stats.HashesCompared}";
    }

    /// <summary>
    /// Debug text: one line for each value, numbers with a fixed width. With the monospace font of the label,
    /// the columns do not move when the values change.
    /// </summary>
    private static string BuildDebugText(string sessionDescription, in WorldData world, FighterDefinitionData definition)
    {
        var text = new StringBuilder();
        text.AppendLine(sessionDescription);
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
    /// The setup when the scene runs alone: the exports (stage, player count, seed, session), and for each slot the
    /// next device of keyboard 1, keyboard 2, then the connected controllers. Slots with no real device left get an
    /// idle device (the fighter stands still), so a test match can have more players than devices.
    /// Command-line arguments (<see cref="MatchLaunchArguments"/>) replace the exports.
    /// </summary>
    private MatchSetup CreateDefaultSetup()
    {
        if (StageScene == null)
        {
            throw new InvalidOperationException("MatchRunner: StageScene is not set.");
        }
        if (FighterScene == null)
        {
            throw new InvalidOperationException("MatchRunner: FighterScene is not set.");
        }

        MatchLaunchArguments arguments = MatchLaunchArguments.FromCommandLine();
        foreach (string problem in arguments.Problems)
        {
            GD.PushWarning($"MatchRunner: {problem}");
        }
        int playerCount = arguments.Players ?? PlayerCount;

        _ownInputDevices = new InputDevices();
        IReadOnlyList<InputDevice> devices = _ownInputDevices.All;
        var slotDevices = new List<InputDevice>();
        for (int i = 0; i < playerCount; i++)
        {
            slotDevices.Add(i < devices.Count ? devices[i] : InputDevice.CreateIdle(i + 1 - devices.Count));
        }

        return new MatchSetup
        {
            SlotDevices = slotDevices,
            Seed = arguments.Seed ?? Seed,
            StageScene = StageScene,
            FighterScene = FighterScene,
            SessionType = arguments.SessionType ?? SessionType,
            Loopback = new LoopbackSettings(
                arguments.LatencyMs ?? LoopbackLatencyMs,
                arguments.JitterMs ?? LoopbackJitterMs,
                arguments.LossPercent ?? LoopbackLossPercent),
        };
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


    /// <summary>
    /// Converts the fighter scene to fighter data. The instance is only read and then freed: the views make their own
    /// instances for the visuals. Returns null and logs all errors if the fighter is not valid.
    /// </summary>
    private static FighterDefinitionData? LoadFighter(PackedScene fighterScene)
    {
        Node instance = fighterScene.Instantiate();
        try
        {
            if (instance is not FighterRoot root)
            {
                GD.PushError($"MatchRunner: the root of '{fighterScene.ResourcePath}' is not a FighterRoot.");
                return null;
            }
            FighterDefinitionData fighter = FighterConverter.Convert(root);
            GD.Print($"Fighter '{fighterScene.ResourcePath}' loaded. Fighter hash: {fighter.ComputeHash():X16}");
            return fighter;
        }
        catch (FighterConversionException exception)
        {
            GD.PushError(exception.Message);
            return null;
        }
        finally
        {
            instance.Free();
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
            string debugText = BuildDebugText(SessionDescription(), world, definition);
            DebugLabel.Text = _desyncError == null ? debugText : $"{_desyncError.Message}\n\n{debugText}";
            DebugLabel.Modulate = _desyncError == null ? Colors.White : Colors.OrangeRed;
        }
    }
}
