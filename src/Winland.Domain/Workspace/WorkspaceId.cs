namespace Winland.Domain;

public readonly record struct WorkspaceId(string Name, int Monitor = 0)
{
    public const int SpecialStart = -99;

    public static WorkspaceId Initial { get; } = new("1");

    public static WorkspaceId DefaultSpecial { get; } = new("special:special");

    public bool IsSpecial => Name.StartsWith("special:", StringComparison.Ordinal);

    public const long MaxNumber = uint.MaxValue;

    public bool TryGetNumber(out long number)
    {
        if (long.TryParse(Name, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out number)
            && number > 0
            && number <= MaxNumber
            && number.ToString(System.Globalization.CultureInfo.InvariantCulture) == Name)
            return true;

        number = 0;
        return false;
    }

    public static WorkspaceId FromNumber(long number) => FromNumber(number, MonitorId.Primary);

    public static WorkspaceId FromNumber(long number, MonitorId monitor)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(number, MaxNumber);
        return new(number.ToString(System.Globalization.CultureInfo.InvariantCulture), monitor.Value);
    }
}
