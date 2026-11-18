using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Winland.Application;

public static partial class CommandProtocol
{
    public static string FormatBinds(IReadOnlyList<Bind> binds)
    {
        ArgumentNullException.ThrowIfNull(binds);
        var views = new BindJson[binds.Count];
        for (var i = 0; i < binds.Count; i++)
            views[i] = BindOf(binds[i]);

        return JsonSerializer.Serialize(views);
    }

    public static string FormatBindsText(IReadOnlyList<Bind> binds)
    {
        ArgumentNullException.ThrowIfNull(binds);
        var lines = new string[binds.Count];
        for (var i = 0; i < binds.Count; i++)
            lines[i] = BindText.Line(binds[i]);

        return string.Join('\n', lines);
    }

    public static string FormatErrors(IReadOnlyList<ConfigWarning> warnings)
    {
        ArgumentNullException.ThrowIfNull(warnings);
        var errors = new string[warnings.Count];
        for (var i = 0; i < warnings.Count; i++)
        {
            errors[i] = warnings[i].Line > 0
                ? string.Create(CultureInfo.InvariantCulture, $"line {warnings[i].Line}: {warnings[i].Message}")
                : warnings[i].Message;
        }

        return JsonSerializer.Serialize(errors, Relaxed);
    }

    public static string FormatErrorsText(IReadOnlyList<ConfigWarning> warnings, string? configPath)
    {
        ArgumentNullException.ThrowIfNull(warnings);
        var lines = new string[warnings.Count];
        for (var i = 0; i < warnings.Count; i++)
            lines[i] = ConfigNotice.Describe(warnings[i], configPath);

        return string.Join('\n', lines);
    }

    public static string FormatFiles(string? config, string? log) =>
        JsonSerializer.Serialize(new FilesJson(config, log), Relaxed);

    public static string FormatFilesText(string? config, string? log) =>
        "config: " + (config ?? "none") + "\nlog: " + (log ?? "none");

    internal static int ModMask(Modifier modifiers)
    {
        var mask = 0;
        if (modifiers.HasFlag(Modifier.Shift))
            mask |= 1 << 0;
        if (modifiers.HasFlag(Modifier.Ctrl))
            mask |= 1 << 2;
        if (modifiers.HasFlag(Modifier.Alt))
            mask |= 1 << 3;
        if (modifiers.HasFlag(Modifier.Caps))
            mask |= 1 << 1;
        if (modifiers.HasFlag(Modifier.Mod2))
            mask |= 1 << 4;
        if (modifiers.HasFlag(Modifier.Mod3))
            mask |= 1 << 5;
        if (modifiers.HasFlag(Modifier.Super))
            mask |= 1 << 6;
        if (modifiers.HasFlag(Modifier.Mod5))
            mask |= 1 << 7;

        return mask;
    }

    private static BindJson BindOf(Bind bind)
    {
        var key = bind.Key;
        var keycode = 0;
        if (key.StartsWith("code:", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(key["code:".Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var code)
            && code >= 0)
        {
            key = "";
            keycode = code;
        }

        var mouse = bind.Key.StartsWith("mouse:", StringComparison.OrdinalIgnoreCase);
        return new BindJson(
            mouse,
            bind.Description is not null,
            ModMask(bind.Modifiers),
            key,
            keycode,
            bind.Description ?? "",
            bind.Dispatcher,
            bind.Arguments);
    }

    private static readonly JsonSerializerOptions Relaxed = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private sealed record BindJson(
        [property: JsonPropertyName("mouse")] bool Mouse,
        [property: JsonPropertyName("has_description")] bool HasDescription,
        [property: JsonPropertyName("modmask")] int Modmask,
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("keycode")] int Keycode,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("dispatcher")] string Dispatcher,
        [property: JsonPropertyName("arg")] string Arg);

    private sealed record FilesJson(
        [property: JsonPropertyName("config")] string? Config,
        [property: JsonPropertyName("log")] string? Log);
}
