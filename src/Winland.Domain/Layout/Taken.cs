namespace Winland.Domain;

internal readonly record struct Taken(
    bool Floating,
    bool Minimized,
    Rectangle Bounds,
    string Class = "",
    string Title = "",
    string Exe = "",
    long ProcessId = 0,
    string? InitialClass = null,
    string? InitialTitle = null,
    Size? MinSize = null,
    Size? MaxSize = null,
    double? ScrollingWidth = null,
    FullscreenMode Fullscreen = FullscreenMode.None,
    TiledLimits? TiledLimits = null,
    bool LayoutFullscreen = false);
