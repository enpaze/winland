using Windows.Win32;

namespace Winland.Infrastructure;

internal static class WindowVisibility
{
    public static VisibilityAction Decide(uint @event, WindowFacts facts) => @event switch
    {
        PInvoke.EVENT_OBJECT_HIDE when !facts.Concealed && !facts.Visible && facts.Managed => VisibilityAction.Close,
        PInvoke.EVENT_OBJECT_HIDE when facts.Minimized && facts.Visible => VisibilityAction.UndoMinimize,
        PInvoke.EVENT_OBJECT_HIDE => VisibilityAction.None,

        PInvoke.EVENT_OBJECT_CLOAKED when !facts.Visible && facts.Managed => VisibilityAction.None,
        PInvoke.EVENT_OBJECT_CLOAKED => VisibilityAction.Close,

        PInvoke.EVENT_OBJECT_SHOW or PInvoke.EVENT_OBJECT_UNCLOAKED or PInvoke.EVENT_OBJECT_NAMECHANGE or PInvoke.EVENT_OBJECT_CREATE
            when facts.Minimized => VisibilityAction.None,
        PInvoke.EVENT_OBJECT_NAMECHANGE when facts.Managed => VisibilityAction.Retitle,
        PInvoke.EVENT_OBJECT_SHOW or PInvoke.EVENT_OBJECT_UNCLOAKED or PInvoke.EVENT_OBJECT_NAMECHANGE or PInvoke.EVENT_OBJECT_CREATE
            when facts.Managed => VisibilityAction.Restore,
        PInvoke.EVENT_OBJECT_NAMECHANGE when facts.Declined => VisibilityAction.None,
        PInvoke.EVENT_OBJECT_SHOW or PInvoke.EVENT_OBJECT_UNCLOAKED or PInvoke.EVENT_OBJECT_NAMECHANGE or PInvoke.EVENT_OBJECT_CREATE
            => VisibilityAction.Adopt,
        _ => VisibilityAction.None,
    };
}

internal readonly record struct WindowFacts(bool Minimized, bool Visible, bool Managed, bool Concealed, bool Declined = false);

internal enum VisibilityAction
{
    None,

    UndoMinimize,

    Close,

    Retitle,

    Restore,

    Adopt,
}
