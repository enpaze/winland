namespace Winland.Install;

public static class ManualStartElevation
{
    public const string OptOutVariable = "WINLAND_NO_ELEVATE";

    public const string LoopArgument = "--winland-elevated";

    public const string OptOutArgument = "--no-elevate";

    public static ElevationChoice Decide(
        bool isElevated,
        string? optOutVariable,
        IReadOnlyList<string> arguments,
        bool userInstall = false,
        bool limitedToken = true)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var kept = arguments.Where(argument => !IsLoopArgument(argument) && !IsOptOutArgument(argument)).ToArray();
        if (isElevated || IsOptOut(optOutVariable) || arguments.Any(IsOptOutArgument) || userInstall)
            return new ElevationChoice.Continue(kept);

        if (arguments.Any(IsLoopArgument))
            return new ElevationChoice.Refuse("Winland could not restart with administrator rights.");

        if (!limitedToken)
            return new ElevationChoice.Continue(kept);

        return new ElevationChoice.Relaunch([LoopArgument, .. kept]);
    }

    private static bool IsOptOut(string? variable) =>
        string.Equals(variable?.Trim(), "1", StringComparison.Ordinal);

    private static bool IsLoopArgument(string value) =>
        value.Equals(LoopArgument, StringComparison.OrdinalIgnoreCase);

    private static bool IsOptOutArgument(string value) =>
        value.Equals(OptOutArgument, StringComparison.OrdinalIgnoreCase);
}

public closed record ElevationChoice
{
    private ElevationChoice()
    {
    }

    public sealed record Continue(IReadOnlyList<string> Arguments) : ElevationChoice;

    public sealed record Relaunch(IReadOnlyList<string> Arguments) : ElevationChoice;

    public sealed record Refuse(string Message) : ElevationChoice;
}
