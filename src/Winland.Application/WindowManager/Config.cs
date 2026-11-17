namespace Winland.Application;

public sealed partial class WindowManager
{
    public DebugOptions Debug => _config.Debug;

    public bool RulesMatchTitles => _config.TitleRules;

    public int RuleRevision => Volatile.Read(ref _revision);

    private int _revision;

    private ConfigNotice? _notice;
    private int _noticeGeneration;

    public bool FirstStart { get; set; }

    private Outcome Load(ConfigLoaded loaded) => Apply(loaded.Config);

    private CommandReply ReloadCommand()
    {
        if (ReadConfig is null)
            return new CommandReply("error: config is not available", View([]));

        ConfigRead read;
        try
        {
            read = ReadConfig();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            read = new ConfigRead(null, new ConfigWarning(0, exception.Message, null));
        }

        if (read.Document is not ConfigDocument document)
        {
            var warning = read.Failure ?? new ConfigWarning(0, "Could not read config.", null);
            var message = warning.Message.StartsWith("error:", StringComparison.Ordinal)
                ? warning.Message
                : "error: " + warning.Message;
            _configEvents = [new ConfigWarned(warning.Line, warning.Message, warning.Source)];
            return new CommandReply(message, View([]));
        }

        _reloaded = document;
        return new CommandReply("ok", Apply(document));
    }

    private Outcome NoteAccent(AccentChanged changed)
    {
        _accent = changed.Color;
        _contrast = changed.Contrast;
        _textScale = changed.TextScale;
        _config = _config.WithSystemColors(_accent, _contrast);
        return View([]);
    }

    private void Use(AppliedConfig config)
    {
        _config = config;
        Interlocked.Increment(ref _revision);
    }

    private Outcome Apply(ConfigDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        Use(AppliedConfig.From(document, _accent, _contrast));
        _loaded = document;
        ShowConfigErrors(document);

        var active = FocusedWorkspace();
        var covered = active.Covered();
        foreach (var desk in _workspaces)
        {
            MatchLayout(desk);
            foreach (var client in desk.Layout.Clients.Concat(desk.WaitingClients))
                _renamed.Add(client.Window);
        }
        Outcome outcome;
        if (_monitors.Count == 0)
        {
            EnsurePersistent();
            IReadOnlyList<Placement> placements = [];
            foreach (var desk in _workspaces)
            {
                Stamp(desk);
                var placed = desk.Layout.Apply(desk.Options);
                if (desk.Id == ActiveId())
                    placements = placed;
            }

            DropEmpty();
            outcome = Present(active, covered, placements);
        }
        else
        {
            outcome = SetMonitors(Reported());
            DropEmpty();
        }

        var events = new List<StreamEvent>(document.Warnings.Count + 1);
        foreach (var warning in document.Warnings)
            events.Add(new ConfigWarned(warning.Line, warning.Message, warning.Source));

        events.Add(new ConfigReloaded());

        events.Add(new MonitorLayoutChanged());
        _configEvents = events;
        return outcome;
    }

    private void ShowConfigErrors(ConfigDocument document)
    {
        var generation = _noticeGeneration + 1;
        var next = ConfigNotice.For(document.Warnings, document.Path, generation);
        if (next is null && FirstStart && _noticeGeneration == 0)
            next = ConfigNotice.Welcome(document.Path, generation);

        if (next is null)
        {
            _notice = null;
            return;
        }

        _noticeGeneration = next.Generation;
        _notice = next;
    }

    private void MatchLayout(LiveWorkspace desk)
    {
        var created = LayoutFactory.Create(_config.Document, RuleFor(desk)?.Layout);
        if (created.GetType() == desk.Layout.GetType() && LayoutFactory.NameOf(created) == LayoutFactory.NameOf(desk.Layout))
        {
            desk.Layout.Configure(_config.Scrolling);
            RankFocus(desk.Layout);
            return;
        }

        AdoptLayout(desk, created);
    }

    private void AdoptLayout(LiveWorkspace desk, Layout created)
    {
        RankFocus(created);
        if (desk.Layout.Clients.Count == 0)
        {
            desk.Replace(created);
            created.Configure(_config.Scrolling);
            return;
        }

        var held = new List<(WindowId Window, Taken Taken)>();
        foreach (var client in desk.Layout.Clients.ToArray())
        {
            if (desk.Take(client.Window, out var taken))
                held.Add((client.Window, taken));
        }

        var focus = desk.Focus;
        desk.Replace(created);
        created.Configure(_config.Scrolling);
        Stamp(desk);
        var screen = MonitorById(desk.Monitor);
        var area = WorkArea(desk, _area);
        created.Apply(desk.Options);
        if (area.Width > 0 && area.Height > 0)
            created.Fit(area, screen is { Arranged.Enabled: true } ? screen.Bounds : default);

        var monitor = screen is { Arranged.Enabled: true } ? screen.Bounds : default;
        foreach (var (window, taken) in held)
            desk.Receive(window, taken, area, monitor);

        if (focus is WindowId focused)
            created.NoteFocus(focused);
    }
}
