namespace Aw.Sindri.Core.Line;

/// <summary>
/// The engine every line station's checks share (line spec §4.3, §6.3): runs each rule of the stage over the files, reports a rule whose files are absent as
/// not-run with their names (never a failure; at the evidence stage never a pass), and folds the findings into an outcome.
/// </summary>
public static class LineChecks
{
    /// <summary>Runs the rules of a station.</summary>
    /// <param name="station">'design' or 'plan'.</param>
    /// <param name="rules">The station's rules in the order the checklist shows them.</param>
    /// <param name="facts">The files.</param>
    /// <param name="stage">Draft runs the draft rules; evidence runs all.</param>
    /// <returns>The result.</returns>
    public static LineCheckResponse Run(string station, IReadOnlyList<LineRule> rules, LineFacts facts, LineStage stage)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(facts);
        var results = new List<LineRuleResult>(rules.Count);
        var fails = new List<LineFinding>();
        var warns = new List<LineFinding>();
        foreach (var rule in rules)
        {
            if (rule.From == LineStage.Evidence && stage == LineStage.Draft)
            {
                results.Add(new LineRuleResult(rule.Name, LineRuleStatus.NotRun, []));
                continue;
            }

            var sink = new LineSink(rule.Name, facts);
            rule.Body(facts, sink);
            fails.AddRange(sink.Findings.Where(static f => f.Severity == LineSeverity.Fail));
            if (sink.Missing.Count > 0)
            {
                results.Add(new LineRuleResult(rule.Name, LineRuleStatus.NotRun, sink.Missing));
                continue;
            }

            warns.AddRange(sink.Findings.Where(static f => f.Severity == LineSeverity.Warn));
            results.Add(new LineRuleResult(rule.Name, Status(sink.Findings), []));
        }

        return new LineCheckResponse(station, stage, Outcome(stage, fails, results), fails, warns, results);
    }

    private static LineRuleStatus Status(IReadOnlyList<LineFinding> findings) =>
        findings.Any(static f => f.Severity == LineSeverity.Fail) ? LineRuleStatus.Fail
        : findings.Count > 0 ? LineRuleStatus.Warn
        : LineRuleStatus.Pass;

    private static LineOutcome Outcome(LineStage stage, List<LineFinding> fails, List<LineRuleResult> results)
    {
        if (fails.Count > 0)
        {
            return stage == LineStage.Draft ? LineOutcome.Gaps : LineOutcome.Incomplete;
        }

        if (stage == LineStage.Draft)
        {
            return LineOutcome.Pass;
        }

        return results.Exists(static r => r.Status == LineRuleStatus.NotRun) ? LineOutcome.Incomplete : LineOutcome.Complete;
    }
}
