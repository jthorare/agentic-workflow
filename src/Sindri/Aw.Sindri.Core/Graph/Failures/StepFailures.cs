namespace Aw.Sindri.Core.Graph.Failures;

/// <summary>Turns a failed node run's error ("&lt;code&gt;: &lt;message&gt;") into its §6.1 sentence and fix, with the step named by its title (#3304).</summary>
public static class StepFailures
{
    private const string Separator = ": ";

    private const string CodeThrew = "SND-CODE-004";

    private const string NodeHead = "node '";

    // why: the engine's SND-CODE-005 message for a missing runner (CodeRunnerMissing.Message, #3757) and the text it replaces; both mean the binary is not installed
    private static readonly string[] _runnerMissingLeads = ["Runner binary not found at ", "the runner binary is not at "];

    // why: SND-CODE-005 covers every way the runner can fail to start; only a missing binary has a fix the owner can run, so it gets its own sentence (spec §6.1)
    private static readonly FailureSentence _runnerMissing = new(
        "SND-CODE-005", "Sindri can't run code steps: sindri-code-runner isn't installed.", "Install the code runner", "scripts/install-cs.sh");

    /// <summary>The sentence and fix for a failed step.</summary>
    /// <param name="step">The step's title, never its node id.</param>
    /// <param name="error">The node run's error as the engine wrote it.</param>
    /// <param name="raw">The model's text of a failed prompt step, or null.</param>
    /// <returns>The failure in words, or null when the code has no row or the row needs a value the error does not carry.</returns>
    public static StepFailure? Describe(string step, string error, string? raw = null)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(error);
        var at = error.IndexOf(Separator, StringComparison.Ordinal);
        if (at <= 0 || FailureSentences.Find(error[..at]) is not { } row)
        {
            return null;
        }

        var message = error[(at + Separator.Length)..];
        if (string.Equals(row.Code, _runnerMissing.Code, StringComparison.Ordinal) && Array.Exists(_runnerMissingLeads, lead => message.StartsWith(lead, StringComparison.Ordinal)))
        {
            return new StepFailure(row.Code, step, _runnerMissing.Template, _runnerMissing.FixLabel, _runnerMissing.FixCommand, SecondaryLabel: null, error);
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["step"] = step,
            ["message"] = string.Equals(row.Code, CodeThrew, StringComparison.Ordinal) ? WithoutExceptionType(message) : message,
        };
        if (raw is not null)
        {
            values["answer"] = raw;
        }

        return Fills(row.Template, values)
            ? new StepFailure(row.Code, step, FailureSentences.Say(row.Code, values), Label(row.FixLabel, step), row.FixCommand, Label(row.SecondaryLabel, step), error)
            : null;
    }

    /// <summary>The sentence and fix for a run's error ("node '&lt;id&gt;' failed: &lt;code&gt;: &lt;message&gt;", or a loop item's or a stranded continue's), for the run list.</summary>
    /// <param name="error">The run's error as the engine wrote it.</param>
    /// <param name="titles">Each node id of the revision the run walked to its title.</param>
    /// <returns>The failure in words, or null when the error names no known step or its code has no sentence.</returns>
    public static StepFailure? ForRun(string error, IReadOnlyDictionary<string, string> titles)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(titles);
        var close = error.StartsWith(NodeHead, StringComparison.Ordinal) ? error.IndexOf('\'', NodeHead.Length) : -1;
        if (close < 0 || !titles.TryGetValue(error[NodeHead.Length..close], out var title))
        {
            return null;
        }

        // why: what comes between the node and its error differs ("failed", "failed on item 2", "... no 'out' edge to continue on"), so the first ": " that starts a known code wins
        for (var at = error.IndexOf(Separator, close, StringComparison.Ordinal); at >= 0; at = error.IndexOf(Separator, at + Separator.Length, StringComparison.Ordinal))
        {
            if (Describe(title, error[(at + Separator.Length)..]) is { } failure)
            {
                return failure with { Detail = error };
            }
        }

        return null;
    }

    // why: sindri-code-runner writes "<type>: <message>" for an exception the code threw; the type stays in Details, the owner reads the message
    private static string WithoutExceptionType(string message)
    {
        var at = message.IndexOf(Separator, StringComparison.Ordinal);
        var lead = at < 0 ? message.TrimEnd(':', ' ') : message[..at];
        if (!lead.EndsWith("Exception", StringComparison.Ordinal) || !lead.All(static c => char.IsAsciiLetterOrDigit(c) || c is '_' or '.'))
        {
            return message;
        }

        return at < 0 ? string.Empty : message[(at + Separator.Length)..];
    }

    // why: a row whose sentence needs a value the error does not carry (SND-GRAPH-113's field) has no sentence here; the app shows the raw error instead
    private static bool Fills(string template, Dictionary<string, string> values)
    {
        for (var open = template.IndexOf('{'); open >= 0; open = template.IndexOf('{', open + 1))
        {
            var close = template.IndexOf('}', open);
            if (close < 0)
            {
                return true;
            }

            if (!values.ContainsKey(template[(open + 1)..close]))
            {
                return false;
            }
        }

        return true;
    }

    private static string? Label(string? label, string step) => label?.Replace("{step}", step, StringComparison.Ordinal);
}
