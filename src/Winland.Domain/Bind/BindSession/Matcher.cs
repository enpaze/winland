namespace Winland.Domain;

public sealed partial class BindSession
{
    private static readonly Dictionary<string, Modifier> ModifierKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Super_L"] = Modifier.Super,
        ["Super_R"] = Modifier.Super,
        ["Alt_L"] = Modifier.Alt,
        ["Alt_R"] = Modifier.Alt,
        ["Control_L"] = Modifier.Ctrl,
        ["Control_R"] = Modifier.Ctrl,
        ["Shift_L"] = Modifier.Shift,
        ["Shift_R"] = Modifier.Shift,

        ["Caps_Lock"] = Modifier.Caps,
        ["Num_Lock"] = Modifier.Mod2,
        ["ISO_Level3_Shift"] = Modifier.Mod5,
    };

    public BindDecision Decide(BindTable table, Input input)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(input);
        lock (_gate)
            return Match(table, input);
    }

    private BindDecision Match(BindTable table, Input input) =>
        input switch
        {
            KeyPressed pressed => OnPress(table, pressed.Key, pressed.ScanCode, mouse: false),
            KeyReleased released => OnRelease(released.Key, mouse: false),
            PointerButtonPressed button => OnButton(table, button.Button, button.Position, up: false),
            PointerButtonReleased button => OnButton(table, button.Button, button.Position, up: true),
            PointerMoved moved => OnMoved(moved.Position),
            WheelTurned wheel => OnWheel(table, wheel),
            _ => throw new ArgumentOutOfRangeException(nameof(input), input, "No handler for this input."),
        };

    public void Forget(Func<string, bool> stillDown)
    {
        ArgumentNullException.ThrowIfNull(stillDown);
        lock (_gate)
            ForgetMissed(stillDown);
    }

    private void ForgetMissed(Func<string, bool> stillDown)
    {
        Drop(_downKeys, _caughtKeys, _passedKeys, stillDown);
        Drop(_downButtons, _caughtButtons, _passedButtons, stillDown);
        _buttonsHeld = _downButtons.Count;
        _maskedKeys.IntersectWith(_downKeys);

        Held = 0;
        foreach (var key in _downKeys)
        {
            if (ModifierKeys.TryGetValue(key, out var modifier))
                Held |= modifier;
        }
    }

    private static void Drop(HashSet<string> down, HashSet<string> caught, HashSet<string> passed, Func<string, bool> stillDown)
    {
        foreach (var key in down.ToArray())
        {
            if (stillDown(key))
                continue;

            down.Remove(key);
            caught.Remove(key);
            passed.Remove(key);
        }
    }

    private BindDecision OnButton(BindTable table, string button, Point position, bool up)
    {
        Pointer = position;
        return up
            ? OnRelease(button, mouse: true)
            : OnPress(table, button, scanCode: 0, mouse: true);
    }

    private BindDecision OnMoved(Point position)
    {
        Pointer = position;
        return Passed();
    }

    private BindDecision OnPress(BindTable table, string key, int scanCode, bool mouse)
    {
        if (key.Length == 0)
            return Passed();

        var down = mouse ? _downButtons : _downKeys;
        var repeat = !down.Add(key);
        if (mouse)
            _buttonsHeld = _downButtons.Count;

        if (!mouse && ModifierKeys.TryGetValue(key, out var modifier))
            Held |= modifier;

        if (repeat)
            return (mouse ? _caughtButtons : _caughtKeys).Contains(key) ? Caught() : Passed();

        var binds = Collect(table, key, scanCode, mouse);
        if (binds.Count == 0)
        {
            (mouse ? _passedButtons : _passedKeys).Add(key);
            return Passed();
        }

        Catch(key, mouse);
        return MaskHeld(Ran(binds));
    }

    private BindDecision OnRelease(string key, bool mouse)
    {
        var down = mouse ? _downButtons : _downKeys;
        if (key.Length == 0 || !down.Remove(key))
            return Passed();

        if (mouse)
            _buttonsHeld = _downButtons.Count;
        else
            ReleaseModifier(key);

        var caught = (mouse ? _caughtButtons : _caughtKeys).Remove(key);
        var passed = (mouse ? _passedButtons : _passedKeys).Remove(key);
        if (!mouse)
            _maskedKeys.Remove(key);

        return caught && !passed ? Caught() : Passed();
    }

    private BindDecision MaskHeld(BindDecision decision)
    {
        var mask = false;
        foreach (var key in _passedKeys)
        {
            if (ModifierKeys.TryGetValue(key, out var modifier) && modifier is Modifier.Super or Modifier.Alt && _maskedKeys.Add(key))
                mask = true;
        }

        return mask ? decision with { Mask = true } : decision;
    }

    private BindDecision OnWheel(BindTable table, WheelTurned wheel)
    {
        if (wheel.Key.Length == 0)
            return Passed();

        var binds = Collect(table, wheel.Key, scanCode: 0, mouse: false);
        if (binds.Count == 0)
            return Passed();

        if (_scrollSeen && wheel.TimeMs - _scrollAtMs < BindTable.ScrollDelay)
            return Caught();

        _scrollSeen = true;
        _scrollAtMs = wheel.TimeMs;
        return MaskHeld(Ran(binds));
    }

    private static BindDecision Passed() => new(false, null);

    private static BindDecision Caught() => new(true, null);

    private static BindDecision Ran(List<Bind> binds)
    {
        var dispatches = new List<BindDispatch>(binds.Count);
        foreach (var bind in binds)
        {
            if (bind.Dispatcher.Length > 0)
                dispatches.Add(new BindDispatch(bind.Dispatcher, bind.Arguments));
        }

        if (dispatches.Count == 0)
            return Caught();

        return new BindDecision(true, dispatches[0].Dispatcher, dispatches[0].Arguments)
        {
            Dispatches = dispatches.ToArray(),
        };
    }
}
