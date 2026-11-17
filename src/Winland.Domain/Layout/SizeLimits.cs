namespace Winland.Domain;

internal readonly record struct SizeLimits(Size? Min, Size? Max)
{
    public TiledLimits? Tiled { get; init; }

    public SizeLimits ForTiles => Tiled is { } tiled ? new SizeLimits(tiled.Min, tiled.Max) : this;

    public Rectangle Clamp(Rectangle bounds)
    {
        var width = bounds.Width;
        var height = bounds.Height;
        if (Max is { } hi)
        {
            if (hi.Width < int.MaxValue && width > hi.Width)
                width = hi.Width;
            if (hi.Height < int.MaxValue && height > hi.Height)
                height = hi.Height;
        }

        if (Min is { } lo)
        {
            if (lo.Width > 0 && width < lo.Width)
                width = lo.Width;
            if (lo.Height > 0 && height < lo.Height)
                height = lo.Height;
        }

        if (width == bounds.Width && height == bounds.Height)
            return bounds;

        return new Rectangle(bounds.X, bounds.Y, width, height);
    }

    public Rectangle Raise(Rectangle tile, Rectangle area)
    {
        if (Min is not { } lo)
            return tile;

        var width = Math.Max(tile.Width, lo.Width);
        var height = Math.Max(tile.Height, lo.Height);
        if (width == tile.Width && height == tile.Height)
            return tile;

        var x = tile.X + ((tile.Width - width) / 2);
        var y = tile.Y + ((tile.Height - height) / 2);
        x = Math.Clamp(x, area.X, Math.Max(area.X, area.X + area.Width - width));
        y = Math.Clamp(y, area.Y, Math.Max(area.Y, area.Y + area.Height - height));
        return new Rectangle(x, y, width, height);
    }
}

internal readonly record struct TiledLimits(Size? Min, Size? Max);
