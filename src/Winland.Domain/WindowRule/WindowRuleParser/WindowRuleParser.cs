using System.Globalization;
using System.Text.RegularExpressions;

namespace Winland.Domain;

internal static partial class WindowRuleParser
{
    internal static readonly string[] EffectNames =
    [
        "float", "tile", "ignore", "no_focus", "pin", "workspace", "opacity", "fullscreen", "maximize",
        "move", "size", "center", "monitor",
    ];

    private static readonly string[] MatchFields = ["class", "title", "exe"];

    internal static bool TryParse(string value, out WindowRule? rule, out string error)
    {
        rule = null;
        if (value.Split(',').Any(field => field.TrimStart().StartsWith("match:", StringComparison.Ordinal)))
        {
            error = "The match: rule syntax is not supported. Use Winland's form, for example windowrule = float on, class:kitty.";
            return false;
        }

        var comma = value.IndexOf(',');
        if (comma <= 0 || comma == value.Length - 1)
        {
            error = "Malformed window rule.";
            return false;
        }

        if (!TryParseEffect(value[..comma].Trim(), out var effect, out var workspace, out var enabled, out var opacity, out var fullscreen, out var placement, out error))
            return false;

        string? classPattern = null;
        string? titlePattern = null;
        string? exePattern = null;
        foreach (var part in SplitFields(value[(comma + 1)..]))
        {
            if (!TryParseMatch(part, ref classPattern, ref titlePattern, ref exePattern, out error))
                return false;
        }

        if (classPattern is null && titlePattern is null && exePattern is null)
        {
            error = "Malformed window rule.";
            return false;
        }

        rule = new WindowRule(effect, workspace, classPattern, titlePattern, exePattern, enabled, opacity, fullscreen, placement);
        return true;
    }

    internal static WindowDecision Decide(WindowTraits traits, IReadOnlyList<WindowRule> rules)
    {
        bool? adopt = null;
        bool? floating = null;
        string? workspace = null;
        bool? noFocus = null;
        OpacityRule? opacity = null;
        bool? fullscreen = null;
        bool? maximize = null;
        bool? pin = null;
        var placement = default(MapPlacement);
        foreach (var rule in rules)
        {
            if (!Matches(rule, traits))
                continue;

            if (!rule.Enabled && rule.Effect is WindowRuleEffect.Float or WindowRuleEffect.Tile or WindowRuleEffect.Ignore)
                continue;

            switch (rule.Effect)
            {
                case WindowRuleEffect.Ignore:
                    adopt = false;
                    break;
                case WindowRuleEffect.Float:
                    adopt = true;
                    floating = true;
                    break;
                case WindowRuleEffect.Tile:
                    adopt = true;
                    floating = false;
                    break;
                case WindowRuleEffect.Workspace:
                    adopt = true;
                    workspace = rule.Workspace;
                    break;
                case WindowRuleEffect.NoFocus:
                    noFocus = rule.Enabled;
                    break;
                case WindowRuleEffect.Opacity:
                    opacity = rule.Opacity;
                    break;
                case WindowRuleEffect.Fullscreen:
                    fullscreen = rule.Enabled;
                    break;
                case WindowRuleEffect.Maximize:
                    maximize = rule.Enabled;
                    break;
                case WindowRuleEffect.Pin:
                    pin = rule.Enabled;
                    break;
                default:
                    placement = Place(placement, rule);
                    break;
            }
        }

        var sizes = new SizeRule(null, null);
        var limits = sizes.Floating(traits.MinSize, traits.MaxSize);
        var admission = Excluded(traits)
            ?? (adopt == false ? Admission.Ignored
                : Declined(traits) is Admission declined ? (adopt == true ? Admission.Rule : declined)
                : Admission.Default);
        var floatReason = floating is not null ? FloatReason.Rule
            : traits.Topmost ? FloatReason.Topmost
            : traits.Dialog ? FloatReason.Dialog
            : !traits.Resizable ? FloatReason.FixedSize
            : FloatReason.Default;

        return new WindowDecision(
            admission is Admission.Default or Admission.Rule,
            floating ?? floatReason != FloatReason.Default,
            workspace,
            NoFocus: noFocus ?? false,
            MinSize: limits.Min,
            MaxSize: limits.Max,
            Opacity: opacity,
            FullscreenRequest: RequestedFullscreen(fullscreen, maximize),
            Pin: pin ?? traits.Topmost,
            Placement: placement,
            Sizes: sizes,
            Admission: admission,
            FloatReason: floatReason);
    }

    private static bool TryParseEffect(
        string text,
        out WindowRuleEffect effect,
        out string? workspace,
        out bool enabled,
        out OpacityRule? opacity,
        out FullscreenRule? fullscreen,
        out RulePlacement? placement,
        out string error)
    {
        effect = default;
        workspace = null;
        enabled = true;
        opacity = null;
        fullscreen = null;
        placement = null;
        var space = text.IndexOfAny([' ', '\t']);
        var name = space < 0 ? text : text[..space];
        var argument = space < 0 ? null : text[(space + 1)..].Trim();
        if (argument is { Length: 0 })
            argument = null;

        if (name.Length == 0)
        {
            error = "Malformed window rule.";
            return false;
        }

        switch (name)
        {
            case "float":
                effect = WindowRuleEffect.Float;
                break;
            case "tile":
                effect = WindowRuleEffect.Tile;
                break;
            case "ignore":
                effect = WindowRuleEffect.Ignore;
                break;
            case "no_focus":
                effect = WindowRuleEffect.NoFocus;
                break;
            case "pin":
                effect = WindowRuleEffect.Pin;
                break;
            case "workspace":
                if (argument is null)
                {
                    error = "Malformed window rule.";
                    return false;
                }

                if (WorkspaceAddress.IsNamed(argument))
                {
                    error = "Named workspaces are not supported.";
                    return false;
                }

                effect = WindowRuleEffect.Workspace;
                workspace = argument;
                error = "";
                return true;
            case "opacity":
                if (!TryParseOpacity(argument, out var parsedOpacity, out error))
                    return false;

                effect = WindowRuleEffect.Opacity;
                opacity = parsedOpacity;
                error = "";
                return true;
            case "fullscreen":
            case "maximize":
                if (argument is null)
                    enabled = true;
                else if (!TryParseEnabled(argument, out enabled))
                {
                    error = "Malformed window rule.";
                    return false;
                }

                effect = name == "fullscreen" ? WindowRuleEffect.Fullscreen : WindowRuleEffect.Maximize;
                if (enabled)
                {
                    var mode = name == "fullscreen" ? FullscreenMode.Fullscreen : FullscreenMode.Maximized;
                    fullscreen = new FullscreenRule(mode);
                }

                error = "";
                return true;
            default:
                if (TryParsePlacement(name, argument, out effect, out enabled, out placement, out error))
                    return true;

                if (error.Length == 0)
                    error = $"Unsupported window rule '{name}'.{DidYouMean.For(name, EffectNames)}";
                return false;
        }

        if (effect is WindowRuleEffect.Float or WindowRuleEffect.Tile or WindowRuleEffect.Ignore or WindowRuleEffect.NoFocus or WindowRuleEffect.Pin)
        {
            if (argument is null)
            {
                error = "";
                return true;
            }

            if (!TryParseEnabled(argument, out enabled))
            {
                error = "Malformed window rule.";
                return false;
            }

            error = "";
            return true;
        }

        if (argument is not null)
        {
            error = "Malformed window rule.";
            return false;
        }

        error = "";
        return true;
    }

    private static bool TryParseOpacity(string? text, out OpacityRule rule, out string error)
    {
        var tokens = string.IsNullOrWhiteSpace(text)
            ? []
            : Tokens.Of(text);
        var fractions = new float[tokens.Length];
        if (tokens.Length == 0)
        {
            rule = default;
            error = "Malformed window rule.";
            return false;
        }

        for (var i = 0; i < tokens.Length; i++)
        {
            if (tokens.Length > 2
                || !float.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out fractions[i])
                || !float.IsFinite(fractions[i]))
            {
                rule = default;
                error = "Malformed window rule.";
                return false;
            }
        }

        rule = fractions.Length switch
        {
            1 => new OpacityRule(fractions[0], fractions[0], fractions[0]),
            _ => new OpacityRule(fractions[0], fractions[1], 1f),
        };
        error = "";
        return true;
    }

    private static FullscreenRule? RequestedFullscreen(bool? fullscreen, bool? maximize)
    {
        if (maximize == true)
            return new FullscreenRule(FullscreenMode.Maximized);

        if (fullscreen == true)
            return new FullscreenRule(FullscreenMode.Fullscreen);

        return null;
    }

    private static bool TryParseEnabled(string text, out bool enabled)
    {
        if (text.Equals("true", StringComparison.OrdinalIgnoreCase)
            || text.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || text.Equals("on", StringComparison.OrdinalIgnoreCase)
            || text == "1")
        {
            enabled = true;
            return true;
        }

        if (text.Equals("false", StringComparison.OrdinalIgnoreCase)
            || text.Equals("no", StringComparison.OrdinalIgnoreCase)
            || text.Equals("off", StringComparison.OrdinalIgnoreCase)
            || text == "0")
        {
            enabled = false;
            return true;
        }

        enabled = false;
        return false;
    }

    private static bool TryParseMatch(
        string text,
        ref string? classPattern,
        ref string? titlePattern,
        ref string? exePattern,
        out string error)
    {
        var colon = text.IndexOf(':');
        if (colon <= 0)
        {
            error = "Malformed window rule.";
            return false;
        }

        var key = text[..colon];
        var value = text[(colon + 1)..];
        if (value.Length == 0)
        {
            error = "Malformed window rule.";
            return false;
        }

        switch (key)
        {
            case "class":
                classPattern = value;
                break;
            case "title":
                titlePattern = value;
                break;
            case "exe":
                exePattern = value;
                break;
            default:
                error = $"Unsupported window rule match '{key}'.{DidYouMean.For(key, MatchFields)}";
                return false;
        }

        error = "";
        return true;
    }

    private static List<string> SplitFields(string text)
    {
        var fields = new List<string>();
        foreach (var piece in text.Split(','))
        {
            if (piece.Trim().Length == 0)
                continue;

            if (fields.Count > 0 && !FieldStart().IsMatch(piece))
                fields[^1] += "," + piece;
            else
                fields.Add(piece);
        }

        return fields.Select(field => field.Trim()).Where(field => field.Length > 0).ToList();
    }

    [GeneratedRegex(@"\A\s*[A-Za-z_]+:")]
    private static partial Regex FieldStart();

    private static bool Matches(WindowRule rule, WindowTraits traits) =>
        Matches(rule.ClassPattern, traits.Class)
        && Matches(rule.TitlePattern, traits.Title)
        && Matches(rule.ExePattern, traits.Exe);

    private static bool Matches(string? pattern, string text) =>
        pattern is null || FieldMatch.Is(pattern, text);

    private static Admission? Declined(WindowTraits traits) =>
        !traits.Titled ? Admission.Untitled
        : traits.Tool ? Admission.Tool
        : traits.Taskbar ? Admission.Shell
        : traits.ExclusiveFullscreen ? Admission.ExclusiveFullscreen
        : null;

    private static Admission? Excluded(WindowTraits traits) =>
        !traits.Visible ? Admission.Hidden
        : !traits.TopLevel ? Admission.Child
        : !traits.Titled && traits.Tool ? Admission.UntitledTool
        : traits.Tooltip ? Admission.Tooltip
        : traits.Cloaked ? Admission.Cloaked
        : traits.Overlay ? Admission.Overlay
        : null;

    internal static string? Draft(bool floating, WindowTraits traits)
    {
        string? field;
        string text;
        if (traits.Exe.Length > 0)
        {
            field = "exe";
            text = traits.Exe;
        }
        else if (traits.Class.Length > 0)
        {
            field = "class";
            text = traits.Class;
        }
        else if (traits.Title.Length > 0)
        {
            field = "title";
            text = traits.Title;
        }
        else
        {
            return null;
        }

        if (!CanDraft(text))
            return null;

        var line = "windowrule = " + (floating ? "float" : "tile") + ", " + field + ":" + text;
        var read = ConfigParser.Parse(line + "\n");
        return read.Warnings.Count == 0 && read.WindowRules is [var rule] && Matches(rule, traits) ? line : null;
    }

    private static bool CanDraft(string text)
    {
        if (text.Length == 0 || text.Trim() != text)
            return false;

        foreach (var c in text)
        {
            if (c is '*' or ',' or '#' or '$' or '{' or '}' or '\\' or ':' or '\n' or '\r')
                return false;
        }

        return true;
    }
}
