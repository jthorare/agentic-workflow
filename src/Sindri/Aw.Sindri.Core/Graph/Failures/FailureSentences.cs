using System.Text;

namespace Aw.Sindri.Core.Graph.Failures;

/// <summary>The one catalog that maps a failure code to its sentence and fix; the UI, the CLI and MCP print the same words (spec §6.1, E9).</summary>
public static class FailureSentences
{
    private const int AnswerLength = 200;

    private const string EditCommand = "sindri wf edit <slug> --base <n>";

    // why: one table, in the order of the spec's §6.1, so a reader sees the whole owner-facing text in one place
    private static readonly Dictionary<string, FailureSentence> _rows = new[]
    {
        Row("GRAPH-CONFIG-001", "{step} needs a repo.", "Open the step", EditCommand + " --ops '[...]' (set the field)"),
        Row("GRAPH-NAME-001", "{connection} is not a connection on this box.", "Connect GitHub", "sindri connection add <label> --preset github"),
        Row("GRAPH-NAME-002", "Couldn't check the connection yet.", "Retry", "sindri wf validate --slug <slug>"),
        Row("GRAPH-TRIGGER-001", "A workflow has one trigger.", "Change trigger", EditCommand + " --ops '[{\"op\":\"replace_node\",...}]'"),
        Row("SND-GRAPH-112", "Only the trigger can be replaced."),
        Row("SND-GRAPH-103", "The model answered with text, not the JSON this step asks for: \"{answer}\".", "Retry with model", "sindri wf test <slug> --with-model", "Open {step}"),
        Row("SND-GRAPH-113", "{step}: \"{field}\" came out empty. {reference} had no value in this input.", "Edit {field}", EditCommand + " --ops '[...]' (set {field})"),
        Row("SND-LAUNCH-003", "The agent for step {step} isn't available: its CLI is not installed or the target is not in the catalog.", "Choose an agent", "sindri profile set workflow.agent.target <id>"),
        Row("SND-LAUNCH-013", "The agent entry can't express {step}'s model or policy.", "Open the step"),
        Row("SND-GRAPH-105", "This workflow is not on.", "Activate", "sindri wf activate <slug>"),
        Row("SND-GRAPH-106", "A manual run's input must be a JSON object.", "Edit the sample", "sindri wf test <slug> --trigger f.json"),
        Row("SND-GRAPH-107", "This run already ended."),
        Row("SND-GRAPH-108", "The output of {step} was cut at 64 KB, so a later step can't read it.", "Open the step"),
        Row("SND-GRAPH-109", "A run of this workflow can't start the workflow again.", "Open the step that does"),
        Row("SND-CODE-001", "{step} doesn't compile: {message}.", "Open {step}"),
        Row("SND-CODE-002", "{step} ran past its time limit.", "Open {step}"),
        Row("SND-CODE-003", "{step} used more memory than its limit.", "Open {step}"),
        Row("SND-CODE-004", "{step} stopped: {message}.", "Open {step}"),
        Row("SND-CODE-005", "Sindri can't run code steps: the code runner couldn't start.\n{message}"),
        Row("SND-GRAPH-007", "Changed elsewhere.", "Reload", "sindri wf show <slug>"),
        Row("SND-API-001", "Sindri is not running.", "Start Sindri", "sindri serve"),
    }.ToDictionary(static r => r.Code, StringComparer.Ordinal);

    private static FailureSentence Row(string code, string template, string? label = null, string? command = null, string? second = null) => new(code, template, label, command, second);

    /// <summary>Finds the row for a code.</summary>
    /// <param name="code">The failure code.</param>
    /// <returns>The row, or null when the code has none.</returns>
    public static FailureSentence? Find(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        return _rows.GetValueOrDefault(code);
    }

    /// <summary>Fills a row's sentence from the finding's step title and config values (never a node id).</summary>
    /// <param name="code">The failure code.</param>
    /// <param name="values">Placeholder name (without braces) to value.</param>
    /// <returns>The sentence with every placeholder filled.</returns>
    /// <exception cref="InvalidOperationException">The code has no row, or a placeholder has no value.</exception>
    public static string Say(string code, IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var row = Find(code) ?? throw new InvalidOperationException($"no failure sentence for '{code}'");
        var text = new StringBuilder();
        var template = row.Template;
        for (var i = 0; i < template.Length; i++)
        {
            var close = template[i] == '{' ? template.IndexOf('}', i) : -1;
            if (close < 0)
            {
                _ = text.Append(template[i]);
                continue;
            }

            var name = template[(i + 1)..close];
            if (!values.TryGetValue(name, out var value))
            {
                throw new InvalidOperationException($"the sentence for '{code}' needs a value for {{{name}}}");
            }

            i = Placeholder(template, close, name, value, text);
        }

        return text.ToString();
    }

    // why: {answer} and {message} are shown on one line and cut; the rest go in as given. Returns the last template index the placeholder used
    private static int Placeholder(string template, int close, string name, string value, StringBuilder text)
    {
        if (string.Equals(name, "message", StringComparison.Ordinal))
        {
            return Message(template, close, Shown(value), text);
        }

        _ = text.Append(string.Equals(name, "answer", StringComparison.Ordinal) ? Shown(value) : value);
        return close;
    }

    private static int Message(string template, int close, string value, StringBuilder text)
    {
        // why: the template puts a period after {message}; a message that ends in a stop of its own would show two ("..", "?.")
        if (close + 1 < template.Length && template[close + 1] == '.' && value.Length > 0)
        {
            if (value[^1] == '.')
            {
                value = value[..^1];
            }
            else if (value[^1] is '?' or '!')
            {
                close++;
            }
        }

        // why: an exception with no message would read "Check stopped: ."; drop the lead-in so it reads "Check stopped."
        if (value.Length == 0)
        {
            DropLeadIn(text);
        }

        _ = text.Append(value);
        return close;
    }

    // why: the text before an empty {message} is its lead-in (": " or a line break); without the message it would dangle
    private static void DropLeadIn(StringBuilder text)
    {
        while (text.Length > 0 && text[^1] is ' ' or '\n')
        {
            text.Length--;
        }

        if (text.Length > 0 && text[^1] == ':')
        {
            text.Length--;
        }
    }

    // why: the model's text sits on one line and shows its first 200 characters, the same cut the block's own message makes
    private static string Shown(string answer)
    {
        var flat = string.Join(' ', answer.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (flat.Length <= AnswerLength)
        {
            return flat;
        }

        var cut = char.IsHighSurrogate(flat[AnswerLength - 1]) ? AnswerLength - 1 : AnswerLength;
        return string.Concat(flat.AsSpan(0, cut), "…");
    }
}
