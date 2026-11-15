namespace Winland.Application;

/// <summary>What a window's rules give it, where the config's value would otherwise apply.</summary>
public sealed partial class WindowManager
{
    // The border a window's rules give it, which the Layouts read through LayoutOptions.ClientBorder.
    private int? ClientBorderOf(ClientId client) =>
        _props.Own(new WindowId(client.Value), WindowProp.BorderSize) is { } border ? (int)Math.Clamp(border, 0, int.MaxValue) : null;
}
