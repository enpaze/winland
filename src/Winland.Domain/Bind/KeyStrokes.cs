using System.Globalization;

namespace Winland.Domain;

public static class KeyStrokes
{
    internal const int FirstMouseButton = 272;

    // mouse:left to mouse:x2 name the buttons from 272 in order.
    private static readonly string[] MouseButtonNames = ["left", "right", "middle", "x1", "x2"];

    /// <summary>The button of <c>mouse:N</c> or a named one such as <c>mouse:left</c>, which is 272.</summary>
    internal static bool TryMouse(string key, out int button)
    {
        button = 0;
        if (!key.StartsWith("mouse:", StringComparison.OrdinalIgnoreCase))
            return false;

        var name = key.AsSpan(6);
        for (var index = 0; index < MouseButtonNames.Length; index++)
        {
            if (name.Equals(MouseButtonNames[index], StringComparison.OrdinalIgnoreCase))
            {
                button = FirstMouseButton + index;
                return true;
            }
        }

        return int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out button);
    }
}
