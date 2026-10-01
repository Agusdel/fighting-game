using System;
using System.Collections.Generic;
using Godot;

namespace FightingGame.PlayerInput;

/// <summary>
/// The input devices that players can use: the keyboard sets, and one device for each connected controller.
/// </summary>
/// <remarks>
/// Controllers use one template action set in the InputMap (<c>controller_left</c>, <c>controller_jump</c>, ...)
/// with "All devices". For each connected controller, this class copies every template action to
/// <c>controller{N}_*</c> (N = Godot device number + 1) with the device number of that controller.
/// So the controller layout is edited in one place, and the number of controllers is not fixed.
/// Dispose the object to stop listening to controller connections.
/// </remarks>
public sealed class InputDevices : IDisposable
{
    private const string ControllerTemplatePrefix = "controller";

    /// <summary>Keyboard sets defined in the InputMap: <c>keyboard1_*</c>, <c>keyboard2_*</c>.</summary>
    private const int KeyboardSetCount = 2;

    private readonly List<InputDevice> _keyboards = new();
    private readonly SortedDictionary<int, InputDevice> _controllers = new();

    public InputDevices()
    {
        for (int i = 1; i <= KeyboardSetCount; i++)
        {
            _keyboards.Add(new InputDevice($"keyboard{i}", $"Keyboard {i}"));
        }
        foreach (int device in Input.GetConnectedJoypads())
        {
            AddController(device);
        }
        Input.Singleton.JoyConnectionChanged += OnJoyConnectionChanged;
    }

    /// <summary>Raised when a controller connects or disconnects.</summary>
    public event Action? DevicesChanged;

    /// <summary>All available devices: the keyboard sets first, then the controllers in device order.</summary>
    public IReadOnlyList<InputDevice> All
    {
        get
        {
            var all = new List<InputDevice>(_keyboards);
            all.AddRange(_controllers.Values);
            return all;
        }
    }

    public void Dispose()
    {
        Input.Singleton.JoyConnectionChanged -= OnJoyConnectionChanged;
    }

    private void OnJoyConnectionChanged(long device, bool connected)
    {
        if (connected)
        {
            AddController((int)device);
        }
        else
        {
            _controllers.Remove((int)device);
        }
        DevicesChanged?.Invoke();
    }

    private void AddController(int device)
    {
        string id = $"{ControllerTemplatePrefix}{device + 1}";
        foreach (string suffix in InputDevice.ActionSuffixes)
        {
            CopyTemplateAction($"{ControllerTemplatePrefix}_{suffix}", $"{id}_{suffix}", device);
        }
        _controllers[device] = new InputDevice(id, $"Controller {device + 1}");
    }

    /// <summary>Creates (or replaces) <paramref name="target"/> with the events of <paramref name="template"/>, bound to one device.</summary>
    private static void CopyTemplateAction(string template, string target, int device)
    {
        if (!InputMap.HasAction(template))
        {
            GD.PushError($"InputDevices: the template action '{template}' is missing in the InputMap.");
            return;
        }

        if (InputMap.HasAction(target))
        {
            InputMap.ActionEraseEvents(target);
        }
        else
        {
            InputMap.AddAction(target, InputMap.ActionGetDeadzone(template));
        }

        foreach (InputEvent templateEvent in InputMap.ActionGetEvents(template))
        {
            var copy = (InputEvent)templateEvent.Duplicate();
            copy.Device = device;
            InputMap.ActionAddEvent(target, copy);
        }
    }
}
