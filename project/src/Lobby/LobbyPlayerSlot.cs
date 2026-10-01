using FightingGame.PlayerInput;
using Godot;

namespace FightingGame.Lobby;

/// <summary>One player slot in the lobby: free ("Press a button to join") or joined (player number, color, device).</summary>
public partial class LobbyPlayerSlot : PanelContainer
{
    private static readonly Color FreeColor = new(1, 1, 1, 0.35f);

    [Export] public ColorRect? ColorBar { get; set; }
    [Export] public Label? TitleLabel { get; set; }
    [Export] public Label? DeviceLabel { get; set; }

    public void ShowFree()
    {
        SetTexts("Press a button\nto join", "", FreeColor);
    }

    public void ShowJoined(int slot, InputDevice device, Color color)
    {
        SetTexts($"Player {slot + 1}", device.DisplayName, color);
    }

    private void SetTexts(string title, string device, Color color)
    {
        if (TitleLabel != null)
        {
            TitleLabel.Text = title;
            TitleLabel.Modulate = color;
        }
        if (DeviceLabel != null)
        {
            DeviceLabel.Text = device;
        }
        if (ColorBar != null)
        {
            ColorBar.Color = color;
        }
    }
}
