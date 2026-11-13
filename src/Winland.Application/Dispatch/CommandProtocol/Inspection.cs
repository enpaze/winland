using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Winland.Application;

// The JSON and text forms of inspect, written from the same record so the two agree.
public static partial class CommandProtocol
{
    internal static string FormatInspect(InspectJson inspected) => JsonSerializer.Serialize(inspected);

    internal static string FormatInspectText(InspectJson inspected)
    {
        if (inspected.Status != Found)
            return "window not found: " + Explain(inspected.Reason);

        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"Window {Bare(inspected.Address ?? "")} -> {Clean(inspected.Title)}:\n")
            .Append(CultureInfo.InvariantCulture, $"\tclass: {Available(inspected.Class)}\n\ttitle: {Available(inspected.Title)}\n")
            .Append(CultureInfo.InvariantCulture, $"\texe: {Available(inspected.Exe)}\n\taumid: {Available(inspected.Aumid)}\n")
            .Append(CultureInfo.InvariantCulture, $"\tpid: {(inspected.Pid is long pid ? pid.ToString(CultureInfo.InvariantCulture) : Unavailable)}\n");

        var at = inspected.ReasonAt == "adoption" ? "when adopted: " : "now: ";
        text.Append(CultureInfo.InvariantCulture, $"\tmanaged: {YesNo(inspected.Managed == true)} ({at}{Explain(inspected.Reason)})\n");
        if (inspected.Floating is bool floating)
        {
            var why = inspected.FloatingReason is { } reason ? at + ExplainFloating(reason) : "changed since it was adopted; the cause is not recorded";
            text.Append(CultureInfo.InvariantCulture, $"\tfloating: {YesNo(floating)} ({why})\n");
        }

        text.Append(CultureInfo.InvariantCulture, $"\trule: {inspected.Rule ?? "none (" + Explain(inspected.RuleNote) + ")"}\n");
        return text.ToString();
    }

    internal const string Found = "found";

    private const string Unavailable = "unavailable";

    private static string Available(string? text) => text is null ? Unavailable : Clean(text);

    // The sentence for each reason code inspect answers with.
    private static string Explain(string? code) => code switch
    {
        "default" => "Winland manages a window like this",
        "rule" => "a window rule decided it",
        "hidden" => "it is not shown",
        "child" => "it is part of another window",
        "untitled_tool" => "it is an untitled tool window, such as a menu",
        "tooltip" => "it is a tooltip",
        "cloaked" => "Windows keeps it out of sight",
        "overlay" => "it is an overlay that takes no input or activation",
        "untitled" => "it has no title",
        "tool" => "it is a tool window",
        "shell" => "it is part of the Windows shell",
        "exclusive_fullscreen" => "it is in exclusive fullscreen",
        "not_adopted" => "a window like this is managed, but Winland has not adopted this one",
        "native" => "no window rule can make it a Client",
        "unwritable" => "none of its class, program, AUMID or title can be written as a rule",
        "no_window" => "no window under the pointer",
        "no_client" => "no Client matches",
        "closed" => "the window closed",
        _ => Unavailable,
    };

    private static string ExplainFloating(string code) => code switch
    {
        "default" => "a window like this tiles",
        "rule" => "a window rule decided it",
        "topmost" => "it stays on top",
        "dialog" => "it is a dialog",
        "fixed_size" => "it cannot be resized",
        "restored" => "Winland restored it as it was before a restart",
        _ => Unavailable,
    };
}

/// <summary>
/// What inspect found. A field Winland could not read, or that does not apply, is null. Reason is a code: why the
/// Window is or is not a Client, or why none was found.
/// </summary>
internal sealed record InspectJson(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("target")] string Target,
    [property: JsonPropertyName("address")] string? Address = null,
    [property: JsonPropertyName("class")] string? Class = null,
    [property: JsonPropertyName("title")] string? Title = null,
    [property: JsonPropertyName("exe")] string? Exe = null,
    [property: JsonPropertyName("aumid")] string? Aumid = null,
    [property: JsonPropertyName("pid")] long? Pid = null,
    [property: JsonPropertyName("managed")] bool? Managed = null,
    [property: JsonPropertyName("floating")] bool? Floating = null,
    [property: JsonPropertyName("reason")] string? Reason = null,
    [property: JsonPropertyName("reasonAt")] string? ReasonAt = null,
    [property: JsonPropertyName("floatingReason")] string? FloatingReason = null,
    [property: JsonPropertyName("rule")] string? Rule = null,
    [property: JsonPropertyName("ruleNote")] string? RuleNote = null);
