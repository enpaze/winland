namespace Winland.Domain;

public static class BindText
{
    public static string Line(Bind bind)
    {
        ArgumentNullException.ThrowIfNull(bind);
        var line = "bind = " + Chord.Format(bind.Modifiers, bind.Key) + ", ";
        if (bind.Description is not null)
            line += "\"" + bind.Description + "\", ";

        line += bind.Dispatcher;
        if (bind.Arguments.Length > 0)
            line += " " + bind.Arguments;

        return line.Replace("#", "##", StringComparison.Ordinal);
    }
}
