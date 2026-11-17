namespace Winland.Domain;

internal static class MotionStyles
{
    internal static Rectangle Window(Rectangle goal)
    {
        var width = Math.Min(5, goal.Width);
        var height = Math.Min(5, goal.Height);
        return new Rectangle(goal.X + ((goal.Width - width) / 2), goal.Y + ((goal.Height - height) / 2), width, height);
    }

    internal static WorkspaceShift Workspace(bool entering, bool left, bool special, Rectangle monitor) =>
        new((entering == left ? 1 : -1) * monitor.Width, 0, special);
}

internal readonly record struct WorkspaceShift(int Dx, int Dy, bool Fades);
