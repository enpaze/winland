namespace Winland.Domain;

internal static class Chord
{
    private static readonly (string Name, Modifier Modifier)[] Modifiers =
    [
        ("Win", Modifier.Win),
        ("Ctrl", Modifier.Ctrl),
        ("Alt", Modifier.Alt),
        ("Shift", Modifier.Shift),
    ];

    internal static string Format(Modifier modifiers, string key)
    {
        var parts = new List<string>();
        foreach (var (name, modifier) in Modifiers)
        {
            if (modifiers.HasFlag(modifier))
                parts.Add(name);
        }

        parts.Add(key);
        return string.Join('+', parts);
    }

    internal static bool TryParse(string text, KeyLayout layout, out Modifier modifiers, out string key, out string error)
    {
        modifiers = Modifier.None;
        key = "";
        error = "";
        var chord = text.Trim();
        if (chord.Contains('$', StringComparison.Ordinal))
        {
            error = $"Undefined variable in chord '{chord}'.";
            return false;
        }

        var parts = chord.Split('+');
        var held = 0;
        while (held < parts.Length - 1 && ModifierNamed(parts[held].Trim()) is var modifier and not Modifier.None)
        {
            modifiers |= modifier;
            held++;
        }

        var keyName = string.Join('+', parts[held..]).Trim();
        if (keyName.Length == 0)
        {
            error = $"Chord '{chord}' is missing a key.";
            return false;
        }

        if (!KeyNames.Matchable(keyName, layout))
        {
            error = held < parts.Length - 1
                ? $"Unknown modifier '{parts[held].Trim()}' in chord '{chord}'. The modifiers are Win, Shift, Ctrl and Alt."
                : $"Unknown key '{keyName}'.";
            return false;
        }

        key = KeyNames.Resolve(keyName, layout);
        return true;
    }

    private static Modifier ModifierNamed(string name) =>
        Array.Find(Modifiers, entry => entry.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Modifier;
}
