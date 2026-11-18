namespace Winland.Domain;

public static partial class ConfigParser
{
    private sealed partial class Parser
    {
        private void HandleBind(string lhs, string rhs, int lineNumber)
        {
            if (!TryParseFlags(lhs["bind".Length..], out var hasDescription, out var flagError))
            {
                Warn(lineNumber, flagError);
                return;
            }

            var minimum = 3 + (hasDescription ? 1 : 0);
            var parts = SplitArgs(rhs, minimum + 1);
            if (parts.Count < minimum)
            {
                Warn(lineNumber, "Bind has too few arguments.");
                return;
            }

            if (parts[0].Contains('$', StringComparison.Ordinal))
            {
                Warn(lineNumber, $"Undefined variable in modifiers '{parts[0].Trim()}'.");
                return;
            }

            if (!ModifierNames.TryParse(parts[0], out var modifiers, out var modifierError))
            {
                Warn(lineNumber, modifierError);
                return;
            }

            var key = KeyNames.Resolve(parts[1], layout);
            if (key.Length == 0)
            {
                Warn(lineNumber, "Bind is missing a key.");
                return;
            }

            if (!KeyNames.Matchable(parts[1], layout))
            {
                Warn(lineNumber, $"Unknown key '{parts[1]}'.");
                return;
            }

            string? description = null;
            var index = 2;
            if (hasDescription)
            {
                description = parts[2];
                index = 3;
            }

            var dispatcher = parts[index];
            var arguments = parts.Count > index + 1 ? parts[index + 1] : "";
            if (dispatcher.Length > 0 && dispatchers is not null && !dispatchers.Contains(dispatcher, StringComparer.OrdinalIgnoreCase))
            {
                Warn(lineNumber, $"Unknown dispatcher '{dispatcher}'.{DidYouMean.For(dispatcher, dispatchers)}");
                return;
            }

            _binds.Add(new Bind(modifiers, key, dispatcher, arguments, description));
        }

        private void HandleUnbind(string rhs, int lineNumber)
        {
            var parts = SplitArgs(rhs, null);
            if (parts.Count == 1 && parts[0] == "all")
            {
                _binds.Add(new UnbindAll());
                return;
            }

            if (parts.Count < 2 || parts[1].Length == 0)
            {
                Warn(lineNumber, "Unbind has too few arguments.");
                return;
            }

            if (!ModifierNames.TryParse(parts[0], out var modifiers, out var modifierError))
            {
                Warn(lineNumber, modifierError);
                return;
            }

            _binds.Add(new Unbind(modifiers, KeyNames.Resolve(parts[1], layout)));
        }

        private static bool TryParseFlags(string flags, out bool hasDescription, out string error)
        {
            hasDescription = false;
            foreach (var flag in flags)
            {
                if (flag != 'd')
                {
                    error = $"Bind flag '{flag}' is not supported.";
                    return false;
                }

                hasDescription = true;
            }

            error = "";
            return true;
        }
    }
}
