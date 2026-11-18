namespace Winland.Domain;

[Flags]
public enum Modifier
{
    None = 0,
    Win = 1 << 0,
    Alt = 1 << 1,
    Ctrl = 1 << 2,
    Shift = 1 << 3,
    CapsLock = 1 << 4,
    NumLock = 1 << 5,
    AltGr = 1 << 6,
}
