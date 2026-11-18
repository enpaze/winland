namespace Winland.Infrastructure;

internal readonly record struct TaskbarChoice(bool Hide, bool Restore, bool Relayout);

internal static class TaskbarPolicy
{
    public static TaskbarChoice Decide(bool hide, bool owns, bool marker, bool open)
    {
        if (hide)
            return owns ? default : new TaskbarChoice(Hide: true, Restore: false, Relayout: open);

        if (owns || marker)
            return new TaskbarChoice(Hide: false, Restore: true, Relayout: open);

        return default;
    }
}

internal interface ITaskbarSession
{
    bool Marker { get; }

    bool ChangesWorkArea { get; }

    void Hide();

    void Restore();
}

public static class TaskbarControl
{
    public static void RecoverHidden()
    {
        if (Taskbar.MarkerPresent())
            Taskbar.Restore();
    }
}

internal sealed class NullTaskbarSession : ITaskbarSession
{
    internal static readonly NullTaskbarSession Instance = new();

    public bool Marker => false;

    public bool ChangesWorkArea => false;

    public void Hide()
    {
    }

    public void Restore()
    {
    }
}

internal sealed class LiveTaskbarSession : ITaskbarSession
{
    internal static readonly LiveTaskbarSession Instance = new();

    public bool Marker => Taskbar.MarkerPresent();

    public bool ChangesWorkArea => true;

    public void Hide() => Taskbar.Hide();

    public void Restore() => Taskbar.Restore();
}
