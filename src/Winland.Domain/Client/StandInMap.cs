namespace Winland.Domain;

public static class StandInMap
{
    private const int EdgeInset = 4;

    public static StandInMapping For(Rectangle frame, Size content, FrameInsets insets, Size? painted)
    {
        var width = painted is Size outer ? Math.Max(1, outer.Width - insets.Left - insets.Right) : Math.Max(1, content.Width);
        var height = painted is Size drawn ? Math.Max(1, drawn.Height - insets.Top - insets.Bottom) : Math.Max(1, content.Height);
        var frameWidth = Math.Max(1, frame.Width);
        var frameHeight = Math.Max(1, frame.Height);

        var scale = Math.Max((double)frameWidth / Math.Max(width, content.Width), (double)frameHeight / Math.Max(height, content.Height));
        var shownWidth = Math.Clamp((int)Math.Round(frameWidth / scale), 1, width);
        var shownHeight = Math.Clamp((int)Math.Round(frameHeight / scale), 1, height);

        if (shownWidth * scale < frameWidth - 0.5)
            shownWidth = Math.Max(1, shownWidth - EdgeInset);
        if (shownHeight * scale < frameHeight - 0.5)
            shownHeight = Math.Max(1, shownHeight - EdgeInset);

        var coveredWidth = Math.Clamp((int)Math.Round(shownWidth * scale), 1, frameWidth);
        var coveredHeight = Math.Clamp((int)Math.Round(shownHeight * scale), 1, frameHeight);

        var picture = new StandInPiece(
            new Rectangle(insets.Left, insets.Top, shownWidth, shownHeight),
            new Rectangle(0, 0, coveredWidth, coveredHeight));

        StandInPiece? right = coveredWidth < frameWidth
            ? new StandInPiece(
                new Rectangle(insets.Left + shownWidth - 1, insets.Top, 1, shownHeight),
                new Rectangle(coveredWidth, 0, frameWidth - coveredWidth, frameHeight))
            : null;
        StandInPiece? bottom = coveredHeight < frameHeight
            ? new StandInPiece(
                new Rectangle(insets.Left, insets.Top + shownHeight - 1, shownWidth, 1),
                new Rectangle(0, coveredHeight, coveredWidth, frameHeight - coveredHeight))
            : null;

        return new StandInMapping(picture, right, bottom);
    }
}

public readonly record struct StandInMapping(StandInPiece Picture, StandInPiece? Right = null, StandInPiece? Bottom = null)
{
    public Rectangle Source => Picture.Source;

    public Rectangle Destination => Picture.Destination;
}

public readonly record struct StandInPiece(Rectangle Source, Rectangle Destination);
