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
        var parts = chord.Split('+');
        var held = 0;
        while (held < parts.Length - 1 && ModifierNamed(parts[held].Trim()) is var modifier and not Modifier.None)
        {
            modifiers |= modifier;
            held++;
        }

        var keyName = string.Join('+', parts[held..]).Trim();
        if (keyName.Length > 0 && KeyNames.Matchable(keyName, layout))
        {
            key = KeyNames.Resolve(keyName, layout);
            return true;
        }

        var failed = held < parts.Length - 1 ? parts[held].Trim() : keyName;
        error = keyName.Length == 0 ? $"Chord '{chord}' is missing a key."
            : failed.Contains('$', StringComparison.Ordinal) ? $"Undefined variable in chord '{chord}'."
            : held < parts.Length - 1 ? $"Unknown modifier '{failed}' in chord '{chord}'. The modifiers are Win, Shift, Ctrl and Alt."
            : $"Unknown key '{keyName}'.";
        return false;
    }

    private static Modifier ModifierNamed(string name) =>
        Array.Find(Modifiers, entry => entry.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Modifier;
}
