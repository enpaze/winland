namespace Winland.Application;

/// <summary><c>inspect</c>: a Window read afresh, whether Winland manages it and why, and a rule that would change that.</summary>
public sealed partial class WindowManager
{
    /// <summary>
    /// Reads a Window afresh for <c>inspect</c>: the one given, or with null the top-level Window under the pointer.
    /// Null when there is none, or it closed. The Windows adapter answers, and changes nothing.
    /// </summary>
    public Func<WindowId?, WindowAppeared?>? DescribeWindow { get; set; }

    // A query: it reads the Window and the State and changes neither.
    private string Inspect(InspectRequest request)
    {
        var pointer = request.Selector.Trim().Length == 0;
        var target = pointer ? "pointer" : "selector";
        WindowId? selected = null;
        if (!pointer)
        {
            // A selector that cannot be read is a mistake in the request, not a Window that is not there.
            if (WindowSelector.Malformed(request.Selector) is { } problem)
                return "error: invalid selector: " + problem;

            if (!TrySelect(request.Selector, out var client, out _))
                return Format(new InspectJson("not_found", target, Reason: "no_client"));

            selected = client.Window;
        }

        if (DescribeWindow is null)
            return "error: no desktop to read windows from";

        // A Window that closed since it was selected reads as nothing, rather than as half of what it was.
        if (DescribeWindow(selected) is not { } read || (selected is WindowId id && read.Window != id))
            return Format(new InspectJson("not_found", target, Reason: pointer ? "no_window" : "closed"));

        var traits = read.Traits;
        var found = new InspectJson(
            CommandProtocol.Found,
            target,
            read.Window.Address,
            Known(traits.Class),
            Known(traits.Title),
            Known(traits.Exe),
            Known(traits.Aumid),
            read.ProcessId > 0 ? read.ProcessId : null);

        return Format(Find(read.Window) is { } managed ? Managed(found, managed, traits) : Unmanaged(found, traits));

        string Format(InspectJson inspected) =>
            Json(request) ? CommandProtocol.FormatInspect(inspected) : CommandProtocol.FormatInspectText(inspected);
    }

    // A Client: why it was adopted is history, so it is said as such. Its floating reason holds only while it still
    // floats or tiles as it did then.
    private InspectJson Managed(InspectJson found, Client client, WindowTraits traits)
    {
        var admitted = _props.AdmittedOf(client.Window);
        var rule = WindowRuleParser.Draft(floating: !client.Floating, traits);
        return found with
        {
            Managed = true,
            Floating = client.Floating,
            Reason = admitted is { } adoption ? Code(adoption.Reason) : null,
            ReasonAt = admitted is null ? null : "adoption",
            FloatingReason = admitted is { } then && then.Floating == client.Floating ? Code(then.FloatReason) : null,
            Rule = rule,
            RuleNote = rule is null ? "unwritable" : null,
        };
    }

    // A Window Winland does not manage: the rules in force now are matched against what was just read. No rule is
    // offered for what Windows itself rules out, nor for the shell, which Winland leaves to Windows.
    private InspectJson Unmanaged(InspectJson found, WindowTraits traits)
    {
        var decision = WindowRuleParser.Decide(traits.WithInitialNames(), _config.WindowRules);
        var native = decision.Admission is Admission.Hidden or Admission.Child or Admission.UntitledTool
            or Admission.Tooltip or Admission.Cloaked or Admission.Overlay or Admission.Shell;
        var rule = decision.Adopt || native ? null : WindowRuleParser.Draft(floating: true, traits);
        return found with
        {
            Managed = false,
            Reason = decision.Adopt ? "not_adopted" : Code(decision.Admission),
            ReasonAt = "now",
            Rule = rule,
            RuleNote = rule is not null ? null : decision.Adopt ? "not_adopted" : decision.Admission == Admission.Shell ? "shell" : native ? "native" : "unwritable",
        };
    }

    private static string? Known(string text) => text.Length == 0 ? null : text;

    private static string Code(Admission reason) => reason switch
    {
        Admission.Default => "default",
        Admission.Rule or Admission.Ignored => "rule",
        Admission.Hidden => "hidden",
        Admission.Child => "child",
        Admission.UntitledTool => "untitled_tool",
        Admission.Tooltip => "tooltip",
        Admission.Cloaked => "cloaked",
        Admission.Overlay => "overlay",
        Admission.Untitled => "untitled",
        Admission.Tool => "tool",
        Admission.Shell => "shell",
        Admission.ExclusiveFullscreen => "exclusive_fullscreen",
        _ => throw new ArgumentOutOfRangeException(nameof(reason)),
    };

    private static string Code(FloatReason reason) => reason switch
    {
        FloatReason.Default => "default",
        FloatReason.Rule => "rule",
        FloatReason.Topmost => "topmost",
        FloatReason.Dialog => "dialog",
        FloatReason.FixedSize => "fixed_size",
        FloatReason.Restored => "restored",
        _ => throw new ArgumentOutOfRangeException(nameof(reason)),
    };
}
