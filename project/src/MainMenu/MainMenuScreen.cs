using System;
using Godot;

namespace FightingGame.MainMenu;

/// <summary>
/// The main menu screen. It only raises events; the app root decides what happens next.
/// </summary>
public partial class MainMenuScreen : Control
{
    [Export] public Button? PlayLocalButton { get; set; }
    [Export] public Button? PlayOnlineButton { get; set; }
    [Export] public Button? QuitButton { get; set; }

    public event Action? PlayLocalPressed;
    public event Action? QuitPressed;

    public override void _Ready()
    {
        if (PlayLocalButton != null)
        {
            PlayLocalButton.Pressed += () => PlayLocalPressed?.Invoke();
            // Focus, so keyboard and controller navigation (ui_* actions) work at once.
            PlayLocalButton.GrabFocus();
        }

        if (PlayOnlineButton != null)
        {
            // Online play comes in a later milestone.
            PlayOnlineButton.Disabled = true;
        }

        if (QuitButton != null)
        {
            QuitButton.Pressed += () => QuitPressed?.Invoke();
        }
    }
}
