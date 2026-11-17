namespace Winland.Infrastructure;

public enum PointerSource
{
    Mouse,

    Injected,

    Touch,

    Pen,
}

public static class PointerSources
{
    private const ulong PenSignature = 0xFF515700;
    private const ulong SignatureMask = 0xFFFFFF00;
    private const ulong TouchBit = 0x80;
    private const uint InjectedFlag = 0x1;

    public static PointerSource Of(uint flags, ulong extra)
    {
        if ((extra & SignatureMask) == PenSignature)
            return (extra & TouchBit) != 0 ? PointerSource.Touch : PointerSource.Pen;

        return (flags & InjectedFlag) != 0 ? PointerSource.Injected : PointerSource.Mouse;
    }
}
