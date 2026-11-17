namespace Winland.Domain;

public static class DecorationAlpha
{
    public static byte Compute(DecorationOptions options, OpacityInput input, OpacityRule? rule, bool opaque)
    {
        ArgumentNullException.ThrowIfNull(options);
        var configured = input.Fullscreen ? 1f
            : input.Active ? options.ActiveOpacity
            : options.InactiveOpacity;
        var factor = rule is null
            ? 1f
            : input.Fullscreen ? rule.Value.Fullscreen
            : input.Active ? rule.Value.Active
            : rule.Value.Inactive;
        return ToByte(opaque ? 1f : factor * configured);
    }

    private static byte ToByte(float fraction)
    {
        if (!float.IsFinite(fraction) || fraction <= 0f)
            return 0;

        var scaled = fraction * 255f;
        if (scaled >= 255f)
            return 255;

        return (byte)Math.Round(scaled, MidpointRounding.AwayFromZero);
    }
}

public readonly record struct OpacityRule(float Active, float Inactive, float Fullscreen);

public readonly record struct OpacityInput(bool Active, bool Fullscreen = false);
