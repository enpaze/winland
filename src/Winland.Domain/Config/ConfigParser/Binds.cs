namespace Winland.Domain;

public static partial class ConfigParser
{
    private sealed partial class Parser
    {
        private void HandleBind(string rhs, int lineNumber)
        {
            var comma = rhs.IndexOf(',', StringComparison.Ordinal);
            if (!Chord.TryParse(comma < 0 ? rhs : rhs[..comma], layout, out var modifiers, out var key, out var chordError))
            {
                Warn(lineNumber, chordError);
                return;
            }

            var rest = comma < 0 ? "" : rhs[(comma + 1)..].Trim();
            string? description = null;
            if (rest.StartsWith('"'))
            {
                var close = rest.IndexOf('"', 1);
                if (close < 0)
                {
                    Warn(lineNumber, "Bind description is missing its closing quote.");
                    return;
                }

                description = rest[1..close];
                rest = rest[(close + 1)..].TrimStart();
                rest = rest.StartsWith(',') ? rest[1..].Trim() : "";
            }

            if (rest.Length == 0)
            {
                Warn(lineNumber, "Bind is missing an action.");
                return;
            }

            var space = rest.IndexOfAny([' ', '\t']);
            var action = space < 0 ? rest : rest[..space];
            var arguments = space < 0 ? "" : rest[space..].Trim();
            if (dispatchers is not null && !dispatchers.Contains(action, StringComparer.OrdinalIgnoreCase))
            {
                Warn(lineNumber, $"Unknown dispatcher '{action}'.{DidYouMean.For(action, dispatchers)}");
                return;
            }

            if (new BindDispatch(action, arguments).IsDrag && !key.StartsWith("mouse:", StringComparison.Ordinal))
            {
                Warn(lineNumber, $"{action} is a drag; bind it to a mouse button.");
                return;
            }

            _binds.Add(new Bind(modifiers, key, action, arguments, description));
        }

        private void HandleUnbind(string rhs, int lineNumber)
        {
            if (rhs.Trim() == "all")
            {
                _binds.Add(new UnbindAll());
                return;
            }

            if (!Chord.TryParse(rhs, layout, out var modifiers, out var key, out var chordError))
            {
                Warn(lineNumber, chordError);
                return;
            }

            _binds.Add(new Unbind(modifiers, key));
        }
    }
}
