using System.Globalization;
using System.Text;
using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Host.Commands.Line;

/// <summary>The text 'sindri design|plan check' prints: a word for every state, no colour.</summary>
internal static class LineText
{
    /// <summary>The outcome line, each failing finding with its fix, the warnings, and the rules that could not run.</summary>
    internal static string Check(LineCheckResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        var text = new StringBuilder();
        var stage = response.Stage == LineStage.Draft ? "draft" : "evidence";
        _ = text.Append(CultureInfo.InvariantCulture, $"{response.Station} {stage}: {Word(response.Outcome)}");
        _ = response.Findings.Count > 0 ? text.Append(CultureInfo.InvariantCulture, $" ({response.Findings.Count} findings)") : text;
        _ = text.Append('\n');
        foreach (var finding in response.Findings)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"{finding.Rule} {finding.Item ?? "-"}: {finding.Message} (fix: {finding.Fix})\n");
        }

        foreach (var warning in response.Warnings)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"warning {warning.Rule} {warning.Item ?? "-"}: {warning.Message}\n");
        }

        foreach (var rule in response.Rules.Where(static r => r.Status == LineRuleStatus.NotRun && r.Missing.Count > 0))
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"not run: {rule.Rule} (needs {string.Join(", ", rule.Missing)})\n");
        }

        return text.ToString();
    }

    private static string N(int number) => number.ToString(CultureInfo.InvariantCulture);

    private static string Word(LineOutcome outcome) => outcome switch
    {
        LineOutcome.Pass => "pass",
        LineOutcome.Gaps => "gaps",
        LineOutcome.Complete => "complete",
        LineOutcome.Incomplete => "incomplete",
        _ => "unknown",
    };

    /// <summary>The stop view as text: the seven parts of line spec §6.4 in the order the owner reads them, a section with no data saying so.</summary>
    internal static string Stop(DesignStopView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var text = new StringBuilder();
        void Heading(string title, string section) => text.Append(CultureInfo.InvariantCulture, $"\n{title}\n").Append(view.NotYet.Contains(section, StringComparer.Ordinal) ? "  not yet\n" : string.Empty);
        void Line(string line) => text.Append("  ").Append(line).Append('\n');
        Heading("Accepted risks", "accepted");
        view.Accepted.ToList().ForEach(a => Line($"{a.Finding} {a.Severity} ({a.Lens}, {a.Cite}): {a.Text}"));
        Heading("Models", "models");
        Line($"writer {view.Models.Writer?.Model ?? "-"}, lens {view.Models.Lens?.Model ?? "-"}");
        view.Models.Warnings.ToList().ForEach(w => Line($"warning: {w}"));
        Heading("Decisions", "decisions");
        view.Decisions.ToList().ForEach(d => Decision(d, Line));
        Heading("Acceptance", "acceptance");
        view.Acceptance.Added.ToList().ForEach(i => Line($"added {i.Item}: {i.Text}"));
        view.Acceptance.Superseded.ToList().ForEach(i => Line($"superseded {i.Item} by {i.SupersededBy}"));
        view.Escapes.ToList().ForEach(e => Line($"escape {e.Kind} {e.Target}: {e.Why}"));
        Heading("Plan graph", "graph");
        Graph(view.Graph, Line);
        Heading("Trace", "trace");
        view.Trace.ToList().ForEach(r => Line($"{r.Item} -> {r.Phase}: {string.Join(", ", r.Signals.Select(static s => $"{s.Signal} {s.Kind} {s.RunsAt}"))}"));
        Heading("Coverage", "coverage");
        view.Coverage.ToList().ForEach(c => Line(c.Excluded is null ? $"{c.Scope} {c.Key}: {string.Join(", ", c.Sections)}" : $"{c.Scope} {c.Key}: excluded, {c.Excluded}"));
        Heading("Spec", "spec");
        view.Spec.Assumes.ToList().ForEach(a => Line($"{a.Section} assumes {a.Option} ({a.Decision})"));
        text.Append(view.Spec.Text);
        Heading("Contract edits", "contracts");
        view.Contracts.ToList().ForEach(c => Line($"{c.Edit} row {N(c.Row)} for {c.Lane} ({c.File}): {string.Join(", ", c.Checks.Select(static k => $"{k.Rule} {k.Status}"))}"));
        Heading("Lens rounds", "lensRounds");
        view.LensRounds.ToList().ForEach(r => Line($"round {N(r.Round)}: {string.Join(", ", r.Lenses.Select(static l => $"{l.Lens} {N(l.Findings.Count)}"))}"));
        if (view.FreezeRefused is { } refused)
        {
            text.Insert(0, $"FREEZE REFUSED {refused.Rule}: {refused.Message}\n");
        }

        return text.ToString();
    }

    private static void Decision(StopDecision d, Action<string> line)
    {
        line($"{d.Decision} ({d.Status}{(d.Chosen is null ? string.Empty : $", chose {d.Chosen}")}, recommended {d.Recommended}): {d.Question}");
        d.Options.ToList().ForEach(o => line($"  {o.Option}{(o.Recommended ? " (recommended)" : string.Empty)}: {o.Answer} | {o.Tradeoffs}"));
    }

    private static void Graph(StopGraph graph, Action<string> line)
    {
        line($"{graph.PrCount.ToString(CultureInfo.InvariantCulture)} PRs, critical path {string.Join(" > ", graph.CriticalPath)}");
        graph.Waves.Select(static (w, i) => $"wave {N(i + 1)}: {string.Join(", ", w)}").ToList().ForEach(line);
        foreach (var p in graph.Phases)
        {
            line($"{p.Phase} [{p.Size}, {p.Kind}] stack {p.Stack.ToString(CultureInfo.InvariantCulture)}, wave {p.Wave.ToString(CultureInfo.InvariantCulture)}, base {p.Base}{(p.WaitsForMerge ? ", waits for merge" : string.Empty)}: {p.Title}");
            line($"  items {string.Join(", ", p.Items)}; files {string.Join(", ", p.AllowedFiles)}; tests {string.Join(", ", p.TestChanges.Select(static t => t.Path))}; {p.HiddenScenarios.ToString(CultureInfo.InvariantCulture)} hidden scenarios");
        }
    }
}
