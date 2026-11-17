namespace Winland.Domain;

public static class BindText
{
    public static string Line(Bind bind)
    {
        ArgumentNullException.ThrowIfNull(bind);
        var keyword = "bind" + Flags(bind);
        var fields = new List<string> { ModifierNames.Format(bind.Modifiers), bind.Key };
        if (bind.Description is not null)
            fields.Add(bind.Description);

        if (bind.Flags.HasFlag(BindFlag.Mouse) && bind.Arguments.Length > 0)
        {
            fields.Add(bind.Dispatcher + " " + bind.Arguments);
        }
        else
        {
            fields.Add(bind.Dispatcher);
            if (bind.Arguments.Length > 0)
                fields.Add(bind.Arguments);
        }

        return keyword + " = " + string.Join(", ", fields).Replace("#", "##", StringComparison.Ordinal);
    }

    private static string Flags(Bind bind)
    {
        var flags = "";
        if (bind.Flags.HasFlag(BindFlag.Mouse))
            flags += 'm';
        if (bind.Description is not null)
            flags += 'd';

        return flags;
    }
}
