using System;
using System.Collections.Generic;
using System.Linq;
using FightingGame.Core;
using FightingGame.PlayerInput;
using FightingGame.Presentation;
using Godot;

namespace FightingGame.Lobby;

/// <summary>
/// The local lobby: players join by pressing a button on their input device. One slot for each possible player
/// (<see cref="GameConstants.MaxPlayers"/>, created in code).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Join: the join button (jump or light attack) of a device that is not in a slot. The device takes the first free slot.</item>
/// <item>Leave: the leave button (heavy attack) of a joined device.</item>
/// <item>Join and leave presses are read in <see cref="_Input"/> and marked as handled, so they never also press a
/// focused button (Space is both a jump key and <c>ui_accept</c>). Other presses reach the UI: a joined player who
/// presses jump again presses the focused Start button.</item>
/// <item><c>ui_cancel</c> (Esc) is the same as the Back button.</item>
/// </list>
/// Call <see cref="Initialize"/> before the screen enters the tree.
/// </remarks>
public partial class LobbyScreen : Control
{
    private static readonly StringName CancelAction = "ui_cancel";

    [Export] public PackedScene? PlayerSlotScene { get; set; }
    [Export] public Container? SlotsContainer { get; set; }
    [Export] public Button? StartButton { get; set; }
    [Export] public Button? BackButton { get; set; }

    private readonly InputDevice?[] _slotDevices = new InputDevice?[GameConstants.MaxPlayers];
    private readonly LobbyPlayerSlot[] _slotViews = new LobbyPlayerSlot[GameConstants.MaxPlayers];
    private InputDevices? _devices;

    /// <summary>Raised with the device of each player slot (index = slot, no gaps).</summary>
    public event Action<IReadOnlyList<InputDevice>>? StartPressed;

    public event Action? BackPressed;

    /// <summary>
    /// Sets the available devices, and the players of the last match (they keep their slots).
    /// Call it before the screen enters the tree.
    /// </summary>
    public void Initialize(InputDevices devices, IReadOnlyList<InputDevice>? previousPlayers)
    {
        _devices = devices;
        if (previousPlayers == null)
        {
            return;
        }

        IReadOnlyList<InputDevice> available = devices.All;
        for (int i = 0; i < previousPlayers.Count && i < _slotDevices.Length; i++)
        {
            // A controller that disconnected during the match does not come back.
            _slotDevices[i] = available.FirstOrDefault(d => d.Id == previousPlayers[i].Id);
        }
        Compact();
    }

    public override void _Ready()
    {
        if (_devices == null)
        {
            throw new InvalidOperationException("LobbyScreen.Initialize must be called before the screen enters the tree.");
        }
        if (PlayerSlotScene == null || SlotsContainer == null)
        {
            throw new InvalidOperationException("LobbyScreen: PlayerSlotScene and SlotsContainer must be set.");
        }

        for (int i = 0; i < _slotViews.Length; i++)
        {
            _slotViews[i] = PlayerSlotScene.Instantiate<LobbyPlayerSlot>();
            SlotsContainer.AddChild(_slotViews[i]);
        }

        if (StartButton != null)
        {
            StartButton.Pressed += OnStartPressed;
            StartButton.GrabFocus();
        }
        if (BackButton != null)
        {
            BackButton.Pressed += () => BackPressed?.Invoke();
        }

        _devices.DevicesChanged += OnDevicesChanged;
        Refresh();
    }

    public override void _ExitTree()
    {
        if (_devices != null)
        {
            _devices.DevicesChanged -= OnDevicesChanged;
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (_devices == null)
        {
            return;
        }

        foreach (InputDevice device in _devices.All)
        {
            int slot = SlotOf(device);
            if (slot < 0 && device.IsJoinPress(@event))
            {
                Join(device);
                GetViewport().SetInputAsHandled();
                return;
            }
            if (slot >= 0 && device.IsLeavePress(@event))
            {
                _slotDevices[slot] = null;
                Compact();
                Refresh();
                GetViewport().SetInputAsHandled();
                return;
            }
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(CancelAction))
        {
            GetViewport().SetInputAsHandled();
            BackPressed?.Invoke();
        }
    }

    private void Join(InputDevice device)
    {
        int free = Array.IndexOf(_slotDevices, null);
        if (free < 0)
        {
            return;
        }
        _slotDevices[free] = device;
        Refresh();
    }

    private int SlotOf(InputDevice device)
    {
        for (int i = 0; i < _slotDevices.Length; i++)
        {
            if (_slotDevices[i]?.Id == device.Id)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>Moves the joined players to the first slots, so the slots of a match have no gaps (Player 1, 2, ...).</summary>
    private void Compact()
    {
        InputDevice[] joined = _slotDevices.Where(d => d != null).Select(d => d!).ToArray();
        for (int i = 0; i < _slotDevices.Length; i++)
        {
            _slotDevices[i] = i < joined.Length ? joined[i] : null;
        }
    }

    private void OnDevicesChanged()
    {
        IReadOnlyList<InputDevice> available = _devices!.All;
        for (int i = 0; i < _slotDevices.Length; i++)
        {
            if (_slotDevices[i] != null && available.All(d => d.Id != _slotDevices[i]!.Id))
            {
                _slotDevices[i] = null;
            }
        }
        Compact();
        Refresh();
    }

    private void Refresh()
    {
        for (int i = 0; i < _slotViews.Length; i++)
        {
            InputDevice? device = _slotDevices[i];
            if (device == null)
            {
                _slotViews[i].ShowFree();
            }
            else
            {
                _slotViews[i].ShowJoined(i, device, PlayerColors.Of(i));
            }
        }

        if (StartButton != null)
        {
            StartButton.Disabled = _slotDevices[0] == null;
        }
    }

    private void OnStartPressed()
    {
        InputDevice[] players = _slotDevices.Where(d => d != null).Select(d => d!).ToArray();
        if (players.Length > 0)
        {
            StartPressed?.Invoke(players);
        }
    }
}
