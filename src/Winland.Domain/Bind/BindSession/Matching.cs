namespace Winland.Domain;

/// <summary>Which binds a press matches: the key and the modifiers held with it.</summary>
public sealed partial class BindSession
{
    // No bind matches while binds are paused, as on the lock screen.
    private List<Bind> Collect(BindTable table, string key, int scanCode, bool mouse)
    {
        var matches = new List<Bind>();
        if (Inhibited)
            return matches;

        var held = MaskFor(key, mouse);
        foreach (var bind in table.Binds)
        {
            if (IsMouseBind(bind) == mouse && bind.Modifiers == held && KeyEquals(bind.Key, key, scanCode, table.Layout))
                matches.Add(bind);
        }

        return matches;
    }

    // A mouse button's bind. A drag bound to a key, through the mouse dispatcher, is the key's.
    private static bool IsMouseBind(Bind bind) =>
        bind.Key.StartsWith("mouse:", StringComparison.OrdinalIgnoreCase);

    private void Catch(string key, bool mouse)
    {
        (mouse ? _caughtButtons : _caughtKeys).Add(key);
        foreach (var held in _downKeys)
        {
            if (ModifierKeys.ContainsKey(held))
                _caughtKeys.Add(held);
        }
    }

    private void ReleaseModifier(string key)
    {
        if (!ModifierKeys.TryGetValue(key, out var modifier))
            return;

        foreach (var pair in ModifierKeys)
        {
            if (pair.Value == modifier && _downKeys.Contains(pair.Key))
                return;
        }

        Held &= ~modifier;
    }

    // A modifier keysym is matched against the mask without that key folded in,
    // unless the other side of the same modifier is already held.
    private Modifier MaskFor(string key, bool mouse)
    {
        if (mouse || !ModifierKeys.TryGetValue(key, out var modifier))
            return Held;

        foreach (var pair in ModifierKeys)
        {
            if (pair.Value != modifier || pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                continue;
            if (_downKeys.Contains(pair.Key))
                return Held;
        }

        return Held & ~modifier;
    }

    // A bound key matches either layout's name, or its scan code when written as code:N.
    private static bool KeyEquals(string bound, string key, int scanCode, KeyLayout layout)
    {
        if (bound.Length == 0)
            return false;
        if (bound.Equals(key, StringComparison.OrdinalIgnoreCase))
            return true;

        if (bound.Equals(layout.NameForCode(scanCode), StringComparison.OrdinalIgnoreCase))
            return true;

        return scanCode > 0 && bound.Equals($"code:{scanCode}", StringComparison.OrdinalIgnoreCase);
    }
}
