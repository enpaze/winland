namespace Winland.Application;

public sealed partial class WindowManager
{
    private readonly FullscreenCover _cover;

    private List<ZOrderRequest> SyncFullscreenHiding() =>
        [.. _cover.Sync(_workspaces, _stacking, window => Find(window) is { Floating: true } && IsShownClient(window))
            .Select(request => new ZOrderRequest(request.Window, request.Top))];
}
