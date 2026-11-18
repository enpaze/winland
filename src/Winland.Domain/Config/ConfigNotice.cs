using System.Globalization;

namespace Winland.Domain;

public sealed record ConfigNotice(string Title, string Text, string? File, int Generation, bool Informational = false)
{
    public static ConfigNotice Welcome(string? configPath, int generation) => new(
        "Winland",
        "Winland is tiling your windows. Run winland binds to list the hotkeys. Right-click the Winland icon to edit the config or quit.",
        string.IsNullOrEmpty(configPath) ? null : configPath,
        generation,
        Informational: true);

    public static ConfigNotice? For(IReadOnlyList<ConfigWarning> warnings, string? configPath, int generation)
    {
        ArgumentNullException.ThrowIfNull(warnings);
        ConfigWarning? first = null;
        var count = 0;
        foreach (var warning in warnings)
        {
            count++;
            first ??= warning;
        }

        if (count == 0 || first is null)
            return null;

        var file = FileOf(first, configPath);
        return new ConfigNotice(
            string.Create(CultureInfo.InvariantCulture, $"Winland: {count} config {(count == 1 ? "error" : "errors")}"),
            Body(first, file),
            file,
            generation);
    }

    public static string Describe(ConfigWarning warning, string? configPath)
    {
        ArgumentNullException.ThrowIfNull(warning);
        return Body(warning, FileOf(warning, configPath));
    }

    public static string? FileOf(ConfigWarning warning, string? configPath)
    {
        var file = string.IsNullOrEmpty(warning.Source) ? configPath : warning.Source;
        return string.IsNullOrEmpty(file) ? null : file;
    }

    private static string Body(ConfigWarning warning, string? file)
    {
        if (!string.IsNullOrEmpty(file) && warning.Line > 0)
            return string.Create(CultureInfo.InvariantCulture, $"{file}:{warning.Line}: {warning.Message}");

        if (warning.Line > 0)
            return string.Create(CultureInfo.InvariantCulture, $"line {warning.Line}: {warning.Message}");

        if (!string.IsNullOrEmpty(file))
            return string.Create(CultureInfo.InvariantCulture, $"{file}: {warning.Message}");

        return warning.Message;
    }
}
