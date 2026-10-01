using System.Collections.Generic;
using FightingGame.MainMenu;
using FightingGame.PlayerInput;
using FightingGame.Presentation;
using Godot;

namespace FightingGame.App;

/// <summary>
/// The app root (main scene). It shows one screen at a time as its child, and changes the screen when a screen
/// raises an event. Screens do not know this class: each screen gets its data as input (for example
/// <see cref="MatchSetup"/>) and reports with events. The app root owns the input devices.
/// </summary>
public partial class Main : Node
{
    [Export] public PackedScene? MainMenuScene { get; set; }
    [Export] public PackedScene? MatchScene { get; set; }

    /// <summary>The stage for local matches.</summary>
    [Export] public PackedScene? StageScene { get; set; }

    private InputDevices? _inputDevices;
    private Node? _currentScreen;

    public override void _Ready()
    {
        _inputDevices = new InputDevices();
        ShowMainMenu();
    }

    public override void _ExitTree()
    {
        _inputDevices?.Dispose();
        _inputDevices = null;
    }

    private void ShowMainMenu()
    {
        var menu = Instantiate<MainMenuScreen>(MainMenuScene, nameof(MainMenuScene));
        menu.PlayLocalPressed += StartLocalMatch;
        menu.QuitPressed += () => GetTree().Quit();
        SetScreen(menu);
    }

    /// <summary>Until the lobby exists: a match with the first two input devices.</summary>
    private void StartLocalMatch()
    {
        IReadOnlyList<InputDevice> devices = _inputDevices!.All;
        var slotDevices = new List<InputDevice>();
        for (int i = 0; i < 2 && i < devices.Count; i++)
        {
            slotDevices.Add(devices[i]);
        }

        StartMatch(new MatchSetup
        {
            SlotDevices = slotDevices,
            Seed = MatchSetup.NewSeed(),
            StageScene = StageScene ?? throw new System.InvalidOperationException("Main: StageScene is not set."),
        });
    }

    private void StartMatch(MatchSetup setup)
    {
        var match = Instantiate<MatchRunner>(MatchScene, nameof(MatchScene));
        match.Setup(setup);
        match.ExitRequested += ShowMainMenu;
        SetScreen(match);
    }

    /// <summary>Removes the current screen (freed at the end of the frame) and adds the new one.</summary>
    private void SetScreen(Node screen)
    {
        if (_currentScreen != null)
        {
            RemoveChild(_currentScreen);
            _currentScreen.QueueFree();
        }
        _currentScreen = screen;
        AddChild(screen);
    }

    private static T Instantiate<T>(PackedScene? scene, string exportName) where T : Node
    {
        if (scene == null)
        {
            throw new System.InvalidOperationException($"Main: {exportName} is not set.");
        }
        return scene.Instantiate<T>();
    }
}
