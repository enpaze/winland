namespace Winland.Domain;

public closed record BindDirective;

public sealed record Bind(
    Modifier Modifiers,
    string Key,
    string Dispatcher,
    string Arguments,
    string? Description) : BindDirective;

public sealed record Unbind(Modifier Modifiers, string Key) : BindDirective;

public sealed record UnbindAll : BindDirective;
