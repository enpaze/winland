using System.Globalization;

namespace Winland.Domain;

internal static class OptionValue
{
    internal static OptionValue<bool> Bool { get; } =
        (string text, out bool value) => ConfigValueSyntax.TryParseBool(text.Trim(), out value);

    internal static OptionValue<string> Text { get; } = (string text, out string value) =>
    {
        value = Unset(text.Trim());
        return true;
    };

    internal static OptionValue<BorderColor> Color { get; } =
        (string text, out BorderColor value) => BorderColor.TryParse(text.Trim(), out value);

    internal static OptionValue<EdgeInsets> Gaps { get; } = TryGaps;

    internal static OptionValue<int> Int(int min = int.MinValue, int max = int.MaxValue, IReadOnlyDictionary<string, int>? names = null) =>
        (string text, out int value) =>
        {
            text = text.Trim();
            if (names is not null && names.TryGetValue(text, out value))
                return true;

            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                return false;

            value = Math.Clamp(value, min, max);
            return true;
        };

    internal static OptionValue<double> Number(double min = double.MinValue, double max = double.MaxValue) =>
        (string text, out double value) =>
        {
            if (!TryDouble(text, out value))
                return false;

            value = Math.Clamp(value, min, max);
            return true;
        };

    internal static OptionValue<float> Single(float min, float max) =>
        (string text, out float value) =>
        {
            value = 0;
            if (!TryDouble(text, out var parsed))
                return false;

            value = Math.Clamp((float)parsed, min, max);
            return true;
        };

    internal static OptionValue<string> OneOf(params string[] choices) =>
        (string text, out string value) =>
        {
            var trimmed = text.Trim();
            value = choices.FirstOrDefault(choice => choice.Equals(trimmed, StringComparison.OrdinalIgnoreCase)) ?? "";
            return value.Length > 0;
        };

    private static string Unset(string text) => text == "[[EMPTY]]" ? "" : text;

    private static bool TryDouble(string text, out double value) =>
        double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);

    private static bool TryGaps(string text, out EdgeInsets gaps)
    {
        gaps = EdgeInsets.Zero;
        var parts = Tokens.Of(text);
        if (parts.Length is 0 or > 4)
            return false;

        var count = parts.Length;
        var values = new int[count];
        for (var i = 0; i < count; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out values[i]))
                return false;

            values[i] = Math.Max(0, values[i]);
        }

        gaps = count switch
        {
            1 => EdgeInsets.Uniform(values[0]),
            2 => new EdgeInsets(values[0], values[1], values[0], values[1]),
            3 => new EdgeInsets(values[0], values[1], values[2], values[1]),
            _ => new EdgeInsets(values[0], values[1], values[2], values[3]),
        };
        return true;
    }
}

internal delegate bool OptionValue<T>(string text, out T value);
