namespace Winland.Domain;

public sealed record AppliedConfig
{
    public static AppliedConfig Unconfigured { get; } = new();

    public ConfigDocument? Document { get; init; }

    public IReadOnlyList<WindowRule> WindowRules => Document?.WindowRules ?? [];

    public bool TitleRules { get; init; }


    public IReadOnlyList<TrackpadGesture> Gestures => Document?.Gestures ?? [];

    public BindTable Binds { get; init; } = BindTable.Empty;

    public LayoutOptions Layout { get; init; } = LayoutOptions.Unconfigured;

    public ScrollingOptions Scrolling { get; init; } = ScrollingOptions.Documented;


    public DecorationOptions Decoration { get; init; } = DecorationOptions.Documented;

    public GroupOptions Groups { get; init; } = GroupOptions.Documented;

    public SessionOptions Session { get; init; } = SessionOptions.Documented;

    public DebugOptions Debug { get; init; } = DebugOptions.Documented;

    public AnimationOptions Animations { get; init; } = AnimationOptions.Documented;

    public CursorOptions Cursor { get; init; } = CursorOptions.Documented;

    public InputOptions Input { get; init; } = InputOptions.Documented;

    public static AppliedConfig From(ConfigDocument document, BorderColor? accent, HighContrast? contrast = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new AppliedConfig
        {
            Document = document,
            TitleRules = document.WindowRules.Any(rule => rule.TitlePattern is not null),
            Binds = BindTable.From(document),
            Layout = LayoutOptions.From(document),
            Scrolling = ScrollingOptions.From(document),
            Decoration = Decorated(document, accent, contrast),
            Groups = GroupOptions.From(document, contrast),
            Session = SessionOptions.From(document),
            Debug = DebugOptions.From(document),
            Animations = AnimationOptions.From(document),
            Cursor = CursorOptions.From(document),
            Input = InputOptions.From(document),
        };
    }

    public AppliedConfig WithSystemColors(BorderColor? accent, HighContrast? contrast)
    {
        var document = Document ?? ConfigDocument.Empty;
        return this with { Decoration = Decorated(document, accent, contrast), Groups = GroupOptions.From(document, contrast) };
    }

    private static DecorationOptions Decorated(ConfigDocument document, BorderColor? accent, HighContrast? contrast)
    {
        var decoration = DecorationOptions.From(document, contrast);
        return contrast is null ? decoration.WithAccent(accent) : decoration;
    }
}
