using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels.Facts;

/// <summary>
/// One finding of a findings fact as the owner reads it (model-check design §4.3): the message first, then where it is. MDL-VIOLATION-001 with a trace in its detail is a list of the steps and the full trace behind
/// a button; any other finding shows its detail, if it has one, as plain text.
/// </summary>
internal sealed class FindingRowViewModel : ObservableObject
{
    private const string ViolationRule = "MDL-VIOLATION-001";
    private const string TraceMark = "Full trace:";

    internal FindingRowViewModel(string rule, string message, string? path, int? line, string? detail)
    {
        Message = FactText.Scrub(message);
        Meta = MetaLine(rule, path, line);
        var text = string.IsNullOrEmpty(detail) ? string.Empty : FactText.Scrub(detail);
        if (string.Equals(rule, ViolationRule, StringComparison.Ordinal) && Trace(text) is { } trace)
        {
            (Steps, FullTrace) = (trace.Steps, trace.Full);
            TraceName = Labels.FullTraceName(trace.States);
        }
        else
        {
            Detail = text;
        }
    }

    /// <summary>What assistive tech reads for the row: the message, then where it is.</summary>
    public string Name => Labels.FindingName(Message, Meta);

    /// <summary>The finding's message, the line the owner reads first.</summary>
    public string Message { get; }

    /// <summary>The path, line and rule, quiet, after the message; empty when there are none.</summary>
    public string Meta { get; }

    /// <summary>True when there is a meta line.</summary>
    public bool HasMeta => Meta.Length > 0;

    /// <summary>A plain finding's detail, as selectable text; empty for a trace finding or one with no detail.</summary>
    public string Detail { get; } = string.Empty;

    /// <summary>True when there is a plain detail.</summary>
    public bool HasDetail => Detail.Length > 0;

    /// <summary>A violation's steps, one per line, in order.</summary>
    public IReadOnlyList<string> Steps { get; } = [];

    /// <summary>True when the finding has a step list and a full trace.</summary>
    public bool HasSteps => Steps.Count > 0;

    /// <summary>The step list's accessible name.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string StepsName => Labels.FindingSteps;
#pragma warning restore CA1822, MA0041

    /// <summary>The expander button's name: "Full trace, N states".</summary>
    public string TraceName { get; } = string.Empty;

    /// <summary>Everything after "Full trace:".</summary>
    public string FullTrace { get; } = string.Empty;

    /// <summary>True while the full trace is shown.</summary>
    public bool IsTraceOpen { get; set => Set(ref field, value); }

    private static string MetaLine(string rule, string? path, int? line)
    {
        var where = string.IsNullOrEmpty(path) ? string.Empty : FactText.Scrub(path) + (line is { } n ? ":" + n.ToString(CultureInfo.InvariantCulture) : string.Empty);
        return string.Join(" · ", new[] { where, FactText.Scrub(rule) }.Where(part => part.Length > 0));
    }

    // TraceSteps.Detail writes the numbered steps, a blank line, "Full trace:" and the raw state blocks; null when the detail has no such mark or no step.
    private static ParsedTrace? Trace(string detail)
    {
        var lines = detail.ReplaceLineEndings("\n").Split('\n');
        var mark = Array.FindIndex(lines, static l => string.Equals(l.TrimEnd(), TraceMark, StringComparison.Ordinal));
        if (mark < 0)
        {
            return null;
        }

        string[] steps = [.. lines[..mark].Select(static l => l.Trim()).Where(static l => l.Length > 0)];
        if (steps.Length == 0)
        {
            return null;
        }

        var full = lines[(mark + 1)..];
        var states = full.Count(IsStateHeader);
        return new ParsedTrace(steps, string.Join('\n', full).Trim(), states > 0 ? states : steps.Length);
    }

    private static bool IsStateHeader(string line) =>
        line.StartsWith("Back to state ", StringComparison.Ordinal) || (line.StartsWith("State ", StringComparison.Ordinal) && line.Length > 6 && char.IsAsciiDigit(line[6]));

    private sealed record ParsedTrace(IReadOnlyList<string> Steps, string Full, int States);
}
