namespace Winland.Domain;

internal static class ModifierNames
{
    private static readonly (string Name, Modifier Modifier)[] Written =
    [
        ("SUPER", Modifier.Super),
        ("SHIFT", Modifier.Shift),
        ("CTRL", Modifier.Ctrl),
        ("ALT", Modifier.Alt),
        ("CAPS", Modifier.Caps),
        ("MOD2", Modifier.Mod2),
        ("MOD3", Modifier.Mod3),
        ("MOD5", Modifier.Mod5),
    ];

    private static readonly (string Name, Modifier Modifier)[] Names =
    [
        .. Written,
        ("CONTROL", Modifier.Ctrl),
        ("MOD1", Modifier.Alt),
        ("WIN", Modifier.Super),
        ("LOGO", Modifier.Super),
        ("MOD4", Modifier.Super),
        ("META", Modifier.Super),
    ];

    internal static string Format(Modifier modifiers)
    {
        var names = new List<string>();
        foreach (var (name, modifier) in Written)
        {
            if (modifiers.HasFlag(modifier))
                names.Add(name);
        }

        return string.Join(' ', names);
    }

    internal static bool TryParse(string text, out Modifier modifiers, out string error)
    {
        modifiers = Modifier.None;
        error = "";
        var upper = text.Trim().ToUpperInvariant();
        if (upper.Length == 0)
            return true;

        foreach (var (name, modifier) in Names)
        {
            if (upper.Contains(name, StringComparison.Ordinal))
                modifiers |= modifier;
        }

        var remainder = upper;
        foreach (var (name, _) in Names.OrderByDescending(entry => entry.Name.Length))
            remainder = remainder.Replace(name, "", StringComparison.Ordinal);

        if (modifiers == Modifier.None || remainder.Any(char.IsLetter))
        {
            error = $"Invalid modifier '{text.Trim()}'.";
            return false;
        }

        return true;
    }
}
