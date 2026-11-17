namespace Winland.Domain;

public static class DisplayColors
{
    public const string StandardPreset = "srgb";

    public static string Format(int bits) => bits switch
    {
        >= 10 => "XRGB2101010",
        > 0 => "XRGB8888",
        _ => "",
    };
}
