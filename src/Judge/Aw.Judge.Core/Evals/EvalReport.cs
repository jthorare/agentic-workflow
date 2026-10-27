using Aw.Judge.Core.Labels;

namespace Aw.Judge.Core.Evals;

/// <summary>One provider's score on one question's labelled items (calls the provider directly: no chain, no decisions rows).</summary>
/// <param name="Question">The question.</param>
/// <param name="Provider">The provider's wire name.</param>
/// <param name="Variant">The input variant, 'as-is' for the stored input.</param>
/// <param name="Labels">The label source scored against, or null for any.</param>
/// <param name="N">How many items were scored.</param>
/// <param name="Decided">How many the provider decided.</param>
/// <param name="Accuracy">Correct decisions over all items.</param>
/// <param name="DecisiveRate">The share of items decided at or above the question's threshold.</param>
/// <param name="AccuracyDecisive">The accuracy on those.</param>
/// <param name="P50LatencyMs">The median latency in milliseconds.</param>
/// <param name="Sweep">The confidence sweep.</param>
/// <param name="PerSource">The results scored per label source, present when the question has both outcome and adjudicator labels on shared items.</param>
/// <param name="SourceWarning">Set when the two sources disagree on more than 30% of shared items.</param>
public sealed record EvalReport(string Question, string Provider, string Variant, LabelSource? Labels, int N, int Decided, double Accuracy, double DecisiveRate, double AccuracyDecisive, long P50LatencyMs, IReadOnlyList<SweepRow> Sweep, SourcedReports? PerSource = null, string? SourceWarning = null);
