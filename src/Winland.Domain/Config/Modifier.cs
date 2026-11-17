namespace Winland.Domain;

[Flags]
public enum Modifier
{
    None = 0,
    Super = 1 << 0,
    Alt = 1 << 1,
    Ctrl = 1 << 2,
    Shift = 1 << 3,

    Caps = 1 << 4,

    Mod2 = 1 << 5,

    Mod3 = 1 << 6,

    Mod5 = 1 << 7,
}
