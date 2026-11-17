namespace Winland.Domain;

public sealed class SwipeRecognizer
{
    public const double PickDistance = 5;

    private bool _failed;
    private double _totalX;
    private double _totalY;

    public TrackpadGesture? Active { get; private set; }

    public void Begin()
    {
        if (Active is not null)
            return;

        _failed = false;
        _totalX = 0;
        _totalY = 0;
    }

    public SwipeStep Update(SwipeUpdated updated, IReadOnlyList<TrackpadGesture> gestures)
    {
        ArgumentNullException.ThrowIfNull(updated);
        ArgumentNullException.ThrowIfNull(gestures);
        if (_failed)
            return default;

        _totalX += updated.DeltaX;
        _totalY += updated.DeltaY;
        var picked = false;
        if (Active is null)
        {
            if (Math.Abs(_totalX) < PickDistance && Math.Abs(_totalY) < PickDistance)
                return default;

            Active = Pick(updated.Fingers, gestures);
            if (Active is null)
            {
                _failed = true;
                return default;
            }

            picked = true;
        }

        return new SwipeStep(picked, Active.Axis == GestureAxis.Horizontal ? updated.DeltaX : updated.DeltaY);
    }

    public TrackpadGesture? End()
    {
        _failed = true;
        var gesture = Active;
        Active = null;
        return gesture;
    }

    private TrackpadGesture? Pick(int fingers, IReadOnlyList<TrackpadGesture> gestures)
    {
        var axis = Math.Abs(_totalX) > Math.Abs(_totalY) ? GestureAxis.Horizontal : GestureAxis.Vertical;
        foreach (var gesture in gestures)
        {
            if (gesture.Axis == axis && gesture.Fingers == fingers)
                return gesture;
        }

        return null;
    }
}

public readonly record struct SwipeStep(bool Picked, double? Along);
