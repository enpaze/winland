using static Winland.Domain.OrdinalText;

namespace Winland.Domain;

public sealed class BindTable
{
    public static BindTable Empty { get; } = new([]);

    private BindTable(Bind[] entries, KeyLayout? layout = null)
    {
        Binds = Array.AsReadOnly(entries);
        Layout = layout ?? KeyLayout.UnitedStates;
    }

    public IReadOnlyList<Bind> Binds { get; }

    internal KeyLayout Layout { get; }

    internal const int ScrollDelay = 300;

    internal static BindTable From(ConfigDocument config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var binds = new List<Bind>();
        foreach (var directive in config.Binds)
        {
            switch (directive)
            {
                case Bind bind:
                    binds.Add(bind);
                    break;
                case Unbind unbind:
                    binds.RemoveAll(bind => bind.Modifiers == unbind.Modifiers && Is(bind.Key, unbind.Key));
                    break;
                case UnbindAll:
                    binds.Clear();
                    break;
            }
        }

        return new BindTable([.. binds], config.KeyLayout);
    }
}
