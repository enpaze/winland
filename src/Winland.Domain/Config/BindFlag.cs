namespace Winland.Domain;

[Flags]
public enum BindFlag
{
    None = 0,

    /// <summary>A drag: the bind moves or resizes the window under the pointer while its button or key is held.</summary>
    Mouse = 1 << 0,
}
