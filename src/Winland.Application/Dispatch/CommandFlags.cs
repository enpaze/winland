namespace Winland.Application;

[Flags]
public enum CommandFlags
{
    None = 0,
    Json = 1 << 0,
}

internal static class CommandFlagReader
{
    internal static CommandFlags Strip(ref string text)
    {
        var slash = text.IndexOf('/');
        if (slash < 0)
            return CommandFlags.None;

        var flags = CommandFlags.None;
        for (var i = 0; i < slash; i++)
        {
            if (text[i] != 'j')
                return CommandFlags.None;

            flags |= CommandFlags.Json;
        }

        text = text[(slash + 1)..].TrimStart();
        return flags;
    }
}
