using System.Text;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// 'workflow status' (engine spec §6.4): the header, then labelled lines in a fixed order — next, run (an agent run, P8), child, inputs, injection, drift,
/// timeout, paused, budget (beside an open desk task, as §6.4 shows it), answer, commands — leaving out the empty ones. Labels take 12 columns; a long 'next' wraps
/// at 120 and every continuation line is indented to the value column. Plain ASCII.
/// </summary>
public static class StatusLayout
{
    private const int Width = 120;
    private const string Indent = "            ";

    /// <summary>The layout of <paramref name="view"/> at <paramref name="now"/>, ending in a newline.</summary>
    /// <param name="view">The instance.</param>
    /// <param name="now">The time ages and timeouts are measured to.</param>
    /// <returns>The text.</returns>
    public static string Render(InstanceView view, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(view);
        var text = new StringBuilder().Append(InstanceHeader.Line(view, now)).Append('\n');
        Labelled(text, "name", NameLines(view));
        Labelled(text, "next", Wrap(view.Next));
        Labelled(text, "run", view.AgentRun is { } run ? [$"agent {run.Launch}"] : []);
        Labelled(text, "child", [.. view.Children.Select(static c => $"{c.Address.Value}  {c.State}")]);
        Labelled(text, "inputs", InputLines(view));
        Labelled(text, "injection", InjectionLines(view));
        Labelled(text, "timings", TimingLines(view));
        Labelled(text, "drift", view.Drift is { } drift ? [drift] : []);
        Labelled(text, "timeout", view.Task?.TimeoutAt is { } at ? [$"parks in {StatusText.Duration(at - now)} (desk)"] : []);
        Labelled(text, "paused", view.Paused is { } left ? [$"timeout {StatusText.Duration(left)} left"] : []);
        Labelled(text, "budget", view is { Task: not null, Budget: { } budget } ? [Budget(budget)] : []);
        Labelled(text, "answer", view.Answer is { } answer ? [answer] : []);
        Labelled(text, "commands", Commands(view) is { Length: > 0 } commands ? [commands] : []);
        return text.ToString();
    }

    /// <summary>The verbs of 'allowedCommands', in their order; an answer forwarded to a child says so.</summary>
    /// <param name="view">The instance.</param>
    /// <returns>'answer, takeover, cancel', or empty for an ended instance.</returns>
    public static string Commands(InstanceView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var forwarded = view.Question is not null && view.Children.Count > 0;
        return string.Join(", ", view.AllowedCommands.Where(static c => c.Verb is null).Select(c => Verb(c.Action) is var verb && verb == "answer" && forwarded ? "answer (forwarded)" : verb).Distinct(StringComparer.Ordinal));
    }

    /// <summary>The verb of an action id: 'complete' for 'CompleteStep', 'answer' for every 'answer-&lt;variant&gt;', else what follows the colon.</summary>
    /// <param name="action">The action id.</param>
    /// <returns>The verb.</returns>
    public static string Verb(string action) => action switch
    {
        "CompleteStep" => "complete",
        _ when action.StartsWith("ApplyCommand:answer", StringComparison.Ordinal) => "answer",
        _ => action[(action.IndexOf(':', StringComparison.Ordinal) + 1)..],
    };

    private static List<string> NameLines(InstanceView view) =>
        view.Name is { } name ? [name.Length > 0 ? name : view.Address.Value] : [];

    private static List<string> InputLines(InstanceView view) =>
        view.Task is { } task ? [.. task.Inputs.Select(static i => $"{i.Key.Value}  {i.Value.Name}  sha256:{StatusText.Digest(i.Value.Value.Sha256)}{(i.Value.Generated ? " (generated)" : string.Empty)}")] : [];

    private static List<string> InjectionLines(InstanceView view) =>
        view.Task is { } task ? [.. task.Injection.Select(static i => $"{i.Section}  {i.Input.Value}  sha256:{i.Sha256[..Math.Min(12, i.Sha256.Length)]}  {i.Bytes.ToString(System.Globalization.CultureInfo.InvariantCulture)} B{(i.Replaced is { } replaced ? $" (replaced {replaced.Value})" : string.Empty)}")] : [];

    private static List<string> TimingLines(InstanceView view) =>
        view.Task?.Timings is { } timings ? [.. timings.Select(static t => $"{t.Input.Value}  {t.DurationMs.ToString(System.Globalization.CultureInfo.InvariantCulture)} ms")] : [];

    private static void Labelled(StringBuilder text, string label, List<string> lines)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            _ = text.Append(i == 0 ? $"  {label,-10}" : Indent).Append(lines[i]).Append('\n');
        }
    }

    /// <summary>'next' as lines: each of its own lines, wrapped at word boundaries to fit the width after the label column.</summary>
    private static List<string> Wrap(string next)
    {
        var lines = new List<string>();
        foreach (var paragraph in next.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = new StringBuilder();
            foreach (var word in paragraph.Split(' '))
            {
                if (line.Length > 0 && Indent.Length + line.Length + 1 + word.Length > Width)
                {
                    lines.Add(line.ToString());
                    _ = line.Clear();
                }

                _ = (line.Length > 0 ? line.Append(' ') : line).Append(word);
            }

            lines.Add(line.ToString());
        }

        return lines;
    }

    private static string Budget(BudgetView budget)
    {
        var scope = budget.PerRound ? "state, this round" : "state";
        List<string> parts =
        [
            budget.StateCap is { } state
                ? $"{StatusText.Tokens(budget.State)} / {StatusText.Tokens(state)} ({scope})"
                : $"{StatusText.Tokens(budget.State)} used ({scope})",   // no cap: "used" only, never "/ <limit>"
            budget.InstanceCap is { } instance
                ? $"{StatusText.Tokens(budget.Instance)} / {StatusText.Tokens(instance)} (instance)"
                : $"{StatusText.Tokens(budget.Instance)} used (instance)",
        ];

        return "tokens " + string.Join(", ", parts) + (budget.Overrun is { } overrun ? $"; over budget: {overrun}" : string.Empty);
    }
}
