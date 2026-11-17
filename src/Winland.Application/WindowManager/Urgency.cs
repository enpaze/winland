namespace Winland.Application;

public sealed partial class WindowManager
{
    private Outcome Flash(WindowId window)
    {
        if (Find(window) is null || _focus.Focused == window)
            return View([]);

        MarkUrgent(window);
        return View([]);
    }

    private void MarkUrgent(WindowId window) =>
        (_extraEvents ??= []).Add(new ClientUrgent(window));
}
