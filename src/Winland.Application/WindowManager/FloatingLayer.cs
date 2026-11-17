namespace Winland.Application;

public sealed partial class WindowManager
{
    private WindowId? _layeredFor;

    private List<ZOrderRequest> FloatingAboveTiles()
    {
        var requests = new List<ZOrderRequest>();
        var active = ActiveWindow();
        if (active == _layeredFor)
            return requests;

        _layeredFor = active;
        if (active is not WindowId focused || Find(focused) is not Client client)
            return requests;

        if (client.Floating)
        {
            Stack(focused);
            return requests;
        }

        foreach (var window in FloatingOver(focused))
            requests.Add(new ZOrderRequest(window, Top: true));

        return requests;
    }

    public IReadOnlyList<WindowId> FloatingAbove(WindowId window)
    {
        lock (_gate)
            return Find(window) is { Floating: false } ? [.. FloatingOver(window)] : [];
    }

    private IEnumerable<WindowId> FloatingOver(WindowId focused) =>
        _stacking.Where(window =>
            window != focused && Find(window) is { Floating: true, Minimized: false } && IsShownClient(window) && !_cover.IsBelow(window));
}
