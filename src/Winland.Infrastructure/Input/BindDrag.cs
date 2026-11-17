namespace Winland.Infrastructure;

internal sealed class BindDrag
{
    internal readonly record struct Active(string Button, string Dispatcher, string Arguments);

    internal Active? Current { get; private set; }

    internal bool Begin(string button, string dispatcher, string arguments)
    {
        if (Current is not null)
            return false;

        Current = new Active(button, dispatcher, arguments);
        return true;
    }

    internal Active? Release(string button)
    {
        if (Current is not Active active || !active.Button.Equals(button, StringComparison.OrdinalIgnoreCase))
            return null;

        Current = null;
        return active;
    }

    internal Active? Abandon()
    {
        var active = Current;
        Current = null;
        return active;
    }
}
