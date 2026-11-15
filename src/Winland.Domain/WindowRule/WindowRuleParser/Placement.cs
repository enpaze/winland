using System.Globalization;

namespace Winland.Domain;

internal static partial class WindowRuleParser
{
    /// <summary>
    /// Parses <c>move</c>, <c>size</c>, <c>center</c> and <c>monitor</c>.
    /// The error is empty when the name is none of them.
    /// </summary>
    private static bool TryParsePlacement(
        string name,
        string? argument,
        out WindowRuleEffect effect,
        out bool enabled,
        out RulePlacement? placement,
        out string error)
    {
        effect = default;
        enabled = true;
        placement = null;
        error = "";
        switch (name)
        {
            case "move":
            case "size":
                if (argument is null || !TryParsePoint(argument, out var point))
                    break;

                effect = name == "move" ? WindowRuleEffect.Move : WindowRuleEffect.Size;
                placement = new RulePlacement(Point: point);
                return true;
            case "center":
                if (argument is not null && !TryParseEnabled(argument, out enabled))
                    break;

                effect = WindowRuleEffect.Center;
                return true;
            case "monitor":
                if (argument is null)
                    break;

                effect = WindowRuleEffect.Monitor;
                placement = new RulePlacement(Monitor: argument);
                return true;
            default:
                return false;
        }

        error = "Malformed window rule.";
        return false;
    }

    // Two lengths. Each is a number of pixels, or a percentage of the work area when it ends in '%'.
    // An expression is not a length, so the rule is skipped.
    private static bool TryParsePoint(string text, out RulePoint point)
    {
        point = default;
        var parts = text.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !TryParseLength(parts[0], out var x) || !TryParseLength(parts[1], out var y))
            return false;

        point = new RulePoint(x, y);
        return true;
    }

    private static bool TryParseLength(string text, out RuleLength length)
    {
        length = default;
        var percent = text.EndsWith('%');
        var number = percent ? text[..^1] : text;
        if (number.Length == 0
            || number.Contains('%')
            || !double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var amount)
            || !double.IsFinite(amount))
            return false;

        length = new RuleLength(amount, percent);
        return true;
    }

    // move and center clear each other, as the last matching one wins.
    private static MapPlacement Place(MapPlacement current, WindowRule rule) => rule.Effect switch
    {
        WindowRuleEffect.Move => current with { Move = rule.Placement?.Point, Center = false },
        WindowRuleEffect.Size => current with { Size = rule.Placement?.Point },
        WindowRuleEffect.Center => current with { Center = rule.Enabled, Move = null },
        WindowRuleEffect.Monitor => current with { Monitor = rule.Placement?.Monitor },
        _ => current,
    };
}
