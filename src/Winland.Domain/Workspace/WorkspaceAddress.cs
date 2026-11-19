using System.Globalization;

namespace Winland.Domain;

internal enum WorkspaceStep
{
    Number,
    Next,
    Prev,
    Last,
    Empty,
    Special,
}

internal readonly record struct WorkspaceAddress(WorkspaceStep Step, long Number = 0, string Special = "")
{
    internal readonly record struct Context(
        WorkspaceId Active,
        Func<WorkspaceId?> Last,
        IReadOnlyList<WorkspaceId> OnMonitor,
        Func<long, bool> NumberIsEmpty);

    internal readonly record struct Rule(WorkspaceId? Id, bool Stay);

    public static bool TryParse(string? text, out WorkspaceAddress address)
    {
        address = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.Trim();
        switch (text.ToLowerInvariant())
        {
            case "next":
                address = new(WorkspaceStep.Next);
                return true;
            case "prev":
                address = new(WorkspaceStep.Prev);
                return true;
            case "last":
                address = new(WorkspaceStep.Last);
                return true;
            case "empty":
                address = new(WorkspaceStep.Empty);
                return true;
            case "special":
                address = new(WorkspaceStep.Special, Special: WorkspaceId.DefaultSpecial.Name);
                return true;
        }

        const string Prefix = "special:";
        if (text.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            var name = text[Prefix.Length..];
            address = new(WorkspaceStep.Special, Special: name.Length == 0 || name == "special" ? WorkspaceId.DefaultSpecial.Name : Prefix + name);
            return true;
        }

        if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < 1 || number > WorkspaceId.MaxNumber)
            return false;

        address = new(WorkspaceStep.Number, number);
        return true;
    }

    public static bool TryParseRule(string? text, out WorkspaceAddress address, out bool stay)
    {
        stay = false;
        var words = (text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 2 && words[1].Equals("stay", StringComparison.OrdinalIgnoreCase))
        {
            stay = true;
            words = words[..1];
        }

        address = default;
        return words.Length == 1 && TryParse(words[0], out address);
    }

    public static Rule ParseRule(string? text, Context context) =>
        TryParseRule(text, out var address, out var stay) && address.Resolve(context) is WorkspaceId id
            ? new(id, stay)
            : new(null, false);

    public WorkspaceId? Resolve(Context context) => Step switch
    {
        WorkspaceStep.Number => WorkspaceId.FromNumber(Number, new MonitorId(context.Active.Monitor)),
        WorkspaceStep.Next => Cycle(context, 1),
        WorkspaceStep.Prev => Cycle(context, -1),
        WorkspaceStep.Last => context.Last(),
        WorkspaceStep.Empty => FirstEmpty(context),
        _ => new WorkspaceId(Special),
    };

    private static WorkspaceId? Cycle(Context context, int step)
    {
        var cycle = context.OnMonitor;
        if (cycle.Count == 0)
            return null;

        var index = -1;
        for (var i = 0; i < cycle.Count; i++)
        {
            if (cycle[i] == context.Active)
            {
                index = i;
                break;
            }
        }

        if (index < 0)
            index = step > 0 ? -1 : 0;

        return cycle[(index + step + cycle.Count) % cycle.Count];
    }

    private static WorkspaceId? FirstEmpty(Context context)
    {
        for (var number = 1L; number < WorkspaceId.MaxNumber; number++)
        {
            if (context.NumberIsEmpty(number))
                return WorkspaceId.FromNumber(number, new MonitorId(context.Active.Monitor));
        }

        return null;
    }
}
