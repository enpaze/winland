using static Winland.Domain.OrdinalText;

namespace Winland.Domain;

public static partial class ConfigParser
{
    private sealed partial class Parser
    {
        private void HandleBind(string lhs, string rhs, int lineNumber)
        {
            if (!TryParseFlags(lhs["bind".Length..], out var flags, out var hasDescription, out var flagError))
            {
                Warn(lineNumber, flagError);
                return;
            }

            var mouse = flags.HasFlag(BindFlag.Mouse);
            var minimum = 3 + (hasDescription ? 1 : 0);
            var parts = mouse ? SplitArgs(rhs, null) : SplitArgs(rhs, minimum + 1);
            if (parts.Count < minimum)
            {
                Warn(lineNumber, "Bind has too few arguments.");
                return;
            }

            if (mouse && parts.Count > minimum)
            {
                Warn(lineNumber, "Bind has too many arguments.");
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
            var arguments = !mouse && parts.Count > index + 1 ? parts[index + 1] : "";
            if (mouse)
            {
                (dispatcher, arguments) = SplitAction(dispatcher);
                if (dispatcher.Length > 0 && !Is(dispatcher, "movewindow") && !Is(dispatcher, "resizewindow"))
                {
                    Warn(lineNumber, "A bindm takes movewindow or resizewindow.");
                    return;
                }
            }
            else if (Is(dispatcher, "mouse"))
            {
                (dispatcher, arguments) = SplitAction(arguments.Trim());
                if (!Is(dispatcher, "movewindow") && !Is(dispatcher, "resizewindow"))
                {
                    Warn(lineNumber, "The mouse dispatcher takes movewindow or resizewindow.");
                    return;
                }

                flags |= BindFlag.Mouse;
            }

            if (dispatcher.Length > 0 && dispatchers is not null && !dispatchers.Contains(dispatcher, StringComparer.OrdinalIgnoreCase))
            {
                Warn(lineNumber, $"Unknown dispatcher '{dispatcher}'.{DidYouMean.For(dispatcher, dispatchers)}");
                return;
            }

            _binds.Add(new Bind(modifiers, key, dispatcher, arguments, flags, description));
        }

        private static (string Dispatcher, string Arguments) SplitAction(string action)
        {
            var space = action.IndexOfAny([' ', '\t']);
            return space > 0 ? (action[..space], action[(space + 1)..].Trim()) : (action, "");
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

        private static bool TryParseFlags(string flags, out BindFlag parsed, out bool hasDescription, out string error)
        {
            parsed = BindFlag.None;
            hasDescription = false;
            foreach (var flag in flags)
            {
                switch (flag)
                {
                    case 'm':
                        parsed |= BindFlag.Mouse;
                        break;
                    case 'd':
                        hasDescription = true;
                        break;
                    default:
                        error = $"Bind flag '{flag}' is not supported.";
                        return false;
                }
            }

            error = "";
            return true;
        }
    }
}
