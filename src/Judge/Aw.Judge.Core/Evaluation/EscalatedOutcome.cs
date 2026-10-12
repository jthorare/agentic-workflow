namespace Aw.Judge.Core.Evaluation;

/// <summary>Nothing decided; the caller escalates to the human or its default.</summary>
/// <param name="ReasonCode">'question-disabled', 'invalid-input', 'below-threshold' or 'no-provider-decided'.</param>
public sealed record EscalatedOutcome(string ReasonCode) : EvaluationOutcome(ReasonCode);
