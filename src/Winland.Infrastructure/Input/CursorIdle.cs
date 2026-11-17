namespace Winland.Infrastructure;

public interface ICursorSurface
{
    void Warp(Point position);

    void Hide();

    void Show();
}

public sealed class CursorIdle
{
    private readonly Lock _gate = new();
    private ICursorSurface _cursor;
    private bool _hideOnKey;
    private bool _touchHidden;
    private bool _keyHidden;
    private bool _hidden;

    public CursorIdle(ICursorSurface cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        _cursor = cursor;
    }

    public void Use(ICursorSurface cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        lock (_gate)
            _cursor = cursor;
    }

    public void Configure(CursorBehavior behavior)
    {
        ArgumentNullException.ThrowIfNull(behavior);
        lock (_gate)
        {
            _hideOnKey = behavior.HideOnKeyPress;
            if (!_hideOnKey)
                _keyHidden = false;
            Publish();
        }
    }

    public void Warp(Point position) => _cursor.Warp(position);

    public void Moved() => Reveal();

    public void Touched()
    {
        lock (_gate)
        {
            _touchHidden = true;
            Publish();
        }
    }

    public void KeyPressed()
    {
        lock (_gate)
        {
            if (!_hideOnKey)
                return;

            _keyHidden = true;
            Publish();
        }
    }

    public void Reveal()
    {
        lock (_gate)
        {
            _keyHidden = false;
            _touchHidden = false;
            Publish();
        }
    }

    private void Publish()
    {
        var hide = _keyHidden || _touchHidden;
        if (hide == _hidden)
            return;

        _hidden = hide;
        if (hide)
            _cursor.Hide();
        else
            _cursor.Show();
    }
}
