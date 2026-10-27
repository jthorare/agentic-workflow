using Aw.Judge.Core.Labels;

namespace Aw.Judge.Core.Evals;

/// <summary>An eval report scored against each label source separately, with how often the sources agree.</summary>
/// <param name="Outcome">The results scored against the outcome labels.</param>
/// <param name="Adjudicator">The results scored against the adjudicator labels.</param>
/// <param name="Agreement">How often the two sources agree on shared items.</param>
public sealed record SourcedReports(EvalReport Outcome, EvalReport Adjudicator, LabelAgreement Agreement);
