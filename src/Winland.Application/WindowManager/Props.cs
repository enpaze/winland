namespace Winland.Application;

public sealed partial class WindowManager
{
    private int? ClientBorderOf(ClientId client) =>
        _props.Own(new WindowId(client.Value), WindowProp.BorderSize) is { } border ? (int)Math.Clamp(border, 0, int.MaxValue) : null;
}
