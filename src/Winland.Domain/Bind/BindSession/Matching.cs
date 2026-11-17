namespace Winland.Domain;

public sealed partial class BindSession
{
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
