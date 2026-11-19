using static Winland.Domain.OrdinalText;

namespace Winland.Application;

internal enum FullscreenAction
{
    Toggle,
    Set,
    Unset,
}

internal readonly record struct FullscreenParse(FullscreenAction Action, bool LayoutAware = true);

internal static class FullscreenCommands
{
    internal const string DefaultHandler = "default";

    internal static FullscreenParse? Parse(string arguments)
    {
        var tokens = arguments.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var layoutAware = !(tokens.Length > 0 && Is(tokens[^1], DefaultHandler));
        if (!layoutAware)
            tokens = tokens[..^1];

        return tokens switch
        {
            [] => new FullscreenParse(FullscreenAction.Toggle, layoutAware),
            [var word] when Is(word, "on") => new FullscreenParse(FullscreenAction.Set, layoutAware),
            [var word] when Is(word, "off") => new FullscreenParse(FullscreenAction.Unset, layoutAware),
            _ => null,
        };
    }

    internal static bool Changed(FullscreenMode current, FullscreenMode mode, FullscreenAction action, out FullscreenMode next)
    {
        switch (action)
        {
            case FullscreenAction.Toggle:
                next = current == mode ? FullscreenMode.None : mode;
                return true;
            case FullscreenAction.Set when current != mode:
                next = mode;
                return true;
            case FullscreenAction.Unset when current == mode:
                next = FullscreenMode.None;
                return true;
            default:
                next = current;
                return false;
        }
    }
}
