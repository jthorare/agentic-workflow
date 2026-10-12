namespace Aw.Judge.Core.Evaluation;

/// <summary>How an evaluation ended: <see cref="DecidedOutcome"/> or <see cref="EscalatedOutcome"/>.</summary>
/// <param name="ReasonCode">Why, e.g. 'pre-rule', 'model', 'below-threshold' or 'no-provider-decided'.</param>
public abstract record EvaluationOutcome(string ReasonCode);
