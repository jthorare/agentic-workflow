using Aw.Judge.Core.Evals;
using Aw.Judge.Core.Labels;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Features.EvaluateQuestion;

/// <summary>Scores provider answers against labels: accuracy, the share decided at the threshold, the confidence sweep and the per-source scores.</summary>
internal static class EvalScoring
{
    internal const string Stalled = "stalled";
    internal const double DisagreementLimit = 0.3;
    internal const string SourceWarning = "label sources disagree on >30% of shared items — neither is ground truth";

    private static readonly double[] _sweep = [0.5, 0.6, 0.7, 0.8, 0.9];

    /// <summary>Folds every label but <paramref name="collapse"/> into 'stalled', in the labels and in the decided answers alike.</summary>
    internal static IReadOnlyList<EvalResult> Collapse(IReadOnlyList<EvalResult> results, string? collapse) =>
        collapse is null ? results : [.. results.Select(r => r with
        {
            Label = Fold(r.Label, collapse),
            Answer = r.Answer is ProviderDecided decided ? decided with { Decision = Fold(decided.Decision, collapse) } : r.Answer,
        })];

    internal static string Fold(string label, string? collapse) =>
        collapse is null || string.Equals(label, collapse, StringComparison.Ordinal) ? label : Stalled;

    internal static EvalReport Score(EvalSetup setup, IReadOnlyList<EvalResult> results, LabelSource? labels)
    {
        var n = results.Count;
        var decided = results.Where(r => r.Answer is ProviderDecided).Select(r => (Row: r, Answer: (ProviderDecided)r.Answer)).ToList();
        bool Correct((EvalResult Row, ProviderDecided Answer) r) => string.Equals(r.Answer.Decision, r.Row.Label, StringComparison.Ordinal);
        List<(EvalResult Row, ProviderDecided Answer)> Covered(double t) => [.. decided.Where(r => r.Answer.Confidence >= t)];
        var decisive = Covered(setup.Threshold);
        var latencies = results.Select(r => r.LatencyMs).Order().ToList();
        return new EvalReport(
            setup.Question,
            setup.Provider,
            setup.Variant,
            labels,
            n,
            decided.Count,
            Ratio(decided.Count(Correct), n),
            Ratio(decisive.Count, n),
            Ratio(decisive.Count(Correct), decisive.Count),
            latencies.Count == 0 ? 0 : latencies[(latencies.Count - 1) / 2],
            [.. _sweep.Select(t =>
            {
                var covered = Covered(t);
                return new SweepRow(t, Ratio(covered.Count, n), Ratio(covered.Count(Correct), covered.Count));
            })]);
    }

    /// <summary>
    /// The report scored against the outcome and the adjudicator labels separately, on the items that have both, with how
    /// often the two agree; the report itself when no scored item has both.
    /// </summary>
    internal static EvalReport WithSources(
        EvalReport report, EvalSetup setup, IReadOnlyList<EvalResult> results, IReadOnlyList<LabeledItem> outcome, IReadOnlyList<LabeledItem> adjudicator)
    {
        var adjudicated = adjudicator.ToDictionary(l => l.Item.Id.Value, l => Fold(l.Label, setup.Collapse), StringComparer.Ordinal);
        var shared = outcome
            .Where(l => adjudicated.ContainsKey(l.Item.Id.Value))
            .ToDictionary(l => l.Item.Id.Value, l => (Outcome: Fold(l.Label, setup.Collapse), Adjudicator: adjudicated[l.Item.Id.Value]), StringComparer.Ordinal);
        var scored = results.Where(r => shared.ContainsKey(r.ItemId)).ToList();
        if (scored.Count == 0)
        {
            return report;
        }

        EvalReport Against(LabelSource source) => Score(
            setup,
            [.. scored.Select(r => r with { Label = source == LabelSource.Outcome ? shared[r.ItemId].Outcome : shared[r.ItemId].Adjudicator })],
            source);
        var agreed = shared.Values.Count(s => string.Equals(s.Outcome, s.Adjudicator, StringComparison.Ordinal));
        var withSources = report with
        {
            PerSource = new SourcedReports(Against(LabelSource.Outcome), Against(LabelSource.Adjudicator), new LabelAgreement(shared.Count, agreed)),
        };
        return (shared.Count - agreed) / (double)shared.Count > DisagreementLimit ? withSources with { SourceWarning = SourceWarning } : withSources;
    }

    internal static double Ratio(int part, int whole) => whole == 0 ? 0 : part / (double)whole;
}
