namespace Winland.Application;

public sealed partial class WindowManager
{
    private readonly Dictionary<WindowId, WindowId> _owners = [];
    private readonly HashSet<WindowId> _disabled = [];

    private bool ShadowedByModal(WindowId window) => _disabled.Contains(window) && _owners.ContainsValue(window);

    private void NoteOwner(WindowAppeared appeared)
    {
        if (appeared.Owner is WindowId owner && owner != appeared.Window)
            _owners[appeared.Window] = owner;

        if (appeared.Disabled)
            _disabled.Add(appeared.Window);
    }

    private void ForgetOwner(WindowId window)
    {
        _owners.Remove(window);
        foreach (var owned in _owners.Where(pair => pair.Value == window).Select(pair => pair.Key).ToArray())
            _owners.Remove(owned);

        _disabled.Remove(window);
    }

    private Outcome ChangeEnabled(WindowEnabledChanged changed)
    {
        if (changed.Enabled)
            _disabled.Remove(changed.Window);
        else
            _disabled.Add(changed.Window);

        return View([]);
    }

}
