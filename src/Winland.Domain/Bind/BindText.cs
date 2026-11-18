namespace Winland.Domain;

public static class BindText
{
    public static string Line(Bind bind)
    {
        ArgumentNullException.ThrowIfNull(bind);
        var fields = new List<string> { ModifierNames.Format(bind.Modifiers), bind.Key };
        if (bind.Description is not null)
            fields.Add(bind.Description);

        fields.Add(bind.Dispatcher);
        if (bind.Arguments.Length > 0)
            fields.Add(bind.Arguments);

        var keyword = bind.Description is null ? "bind" : "bindd";
        return keyword + " = " + string.Join(", ", fields).Replace("#", "##", StringComparison.Ordinal);
    }
}
