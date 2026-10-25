namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// The scoping station's checks (scoping spec §8.2): one pure function over the facts, run by 'step.scope.check' (draft), 'step.scope.evidence',
/// the stop's read model and 'step.scope.freeze'. A rule whose facts are absent is reported not-run with the missing keys and does not fail;
/// at the evidence stage a not-run rule is never a pass.
/// </summary>
internal static partial class ScopePacketChecks
{
    /// <summary>Runs every rule of the stage.</summary>
    /// <param name="facts">The facts.</param>
    /// <param name="stage">Draft runs the draft rules; evidence runs all.</param>
    /// <param name="challengeCap">The running recipe's loops.challenge.cap; <see cref="ScopeRules.DefaultChallengeRounds"/> where no recipe runs the check.</param>
    internal static ScopingCheckResponse Run(ScopeFacts facts, ScopeStage stage, int challengeCap = ScopeRules.DefaultChallengeRounds)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var context = new ScopeContext(facts, challengeCap);
        var rules = new List<ScopeRuleResult>(ScopeRules.All.Count);
        var fails = new List<ScopeFinding>();
        var warns = new List<ScopeFinding>();
        foreach (var (id, from) in ScopeRules.All)
        {
            if (from == ScopeStage.Evidence && stage == ScopeStage.Draft)
            {
                rules.Add(new ScopeRuleResult(id, ScopeRuleStatus.NotRun, []));
                continue;
            }

            var sink = new RuleSink(id, facts);
            Body(id)(context, sink);
            if (sink.Missing.Count > 0)
            {
                rules.Add(new ScopeRuleResult(id, ScopeRuleStatus.NotRun, sink.Missing));
                continue;
            }

            fails.AddRange(sink.Findings.Where(static f => f.Severity == ScopeSeverity.Fail));
            warns.AddRange(sink.Findings.Where(static f => f.Severity == ScopeSeverity.Warn));
            rules.Add(new ScopeRuleResult(id, Status(sink.Findings), []));
        }

        return new ScopingCheckResponse(stage, Outcome(stage, fails, rules), fails, warns, rules);
    }

    private static ScopeRuleStatus Status(IReadOnlyList<ScopeFinding> findings) =>
        findings.Any(static f => f.Severity == ScopeSeverity.Fail) ? ScopeRuleStatus.Fail
        : findings.Count > 0 ? ScopeRuleStatus.Warn
        : ScopeRuleStatus.Pass;

    private static ScopeOutcome Outcome(ScopeStage stage, List<ScopeFinding> fails, List<ScopeRuleResult> rules)
    {
        if (fails.Count > 0)
        {
            // why: SCP-COV-002 fails on a stale lens, which a redraft can't change and a re-embed plus another gather can; SCP-LENS-001 is a fault in the lens itself, which gather reproduces (it fails there first, and the check Step fails on it)
            return fails.TrueForAll(static f => string.Equals(f.Rule, ScopeRules.Cov2, StringComparison.Ordinal))
                ? ScopeOutcome.Regather
                : stage == ScopeStage.Draft ? ScopeOutcome.Gaps : ScopeOutcome.Incomplete;
        }

        if (stage == ScopeStage.Draft)
        {
            return ScopeOutcome.Pass;
        }

        return rules.Exists(static r => r.Status == ScopeRuleStatus.NotRun) ? ScopeOutcome.Incomplete : ScopeOutcome.Complete;
    }

    private static Action<ScopeContext, RuleSink> Body(string id) => id switch
    {
        ScopeRules.Schema => Schema,
        ScopeRules.Cite1 => static (c, s) => Cite(c, s, uncited: true),
        ScopeRules.Cite2 => static (c, s) => Cite(c, s, uncited: false),
        ScopeRules.Cov1 => CoverageRows,
        ScopeRules.Cov2 => CoverageStates,
        ScopeRules.Cov3 => LensItemsMapped,
        ScopeRules.Cov4 => ChangedFilesMapped,
        ScopeRules.Cov5 => ThinMap,
        ScopeRules.Lens1 => LensesMatchCoverage,
        ScopeRules.Acc1 => AcceptanceShape,
        ScopeRules.Acc2 => SurfacesHaveItems,
        ScopeRules.Acc3 => AnswersRecorded,
        ScopeRules.Acc4 => IdsUnique,
        ScopeRules.Acc5 => RegressionItems,
        ScopeRules.Ans1 => AnswersLogged,
        ScopeRules.Trust1 => NoModelTrust,
        ScopeRules.Chal1 => ChallengerDone,
        ScopeRules.Chal2 => ChallengerDiffers,
        ScopeRules.Map1 => Workstreams,
        _ => throw new InvalidOperationException($"No body for rule {id}."),
    };
}
