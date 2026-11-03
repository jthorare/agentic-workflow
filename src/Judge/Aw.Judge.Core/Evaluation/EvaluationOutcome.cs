namespace Aw.Judge.Core.Evaluation;

/// <summary>How an evaluation ended: <see cref="DecidedOutcome"/> or <see cref="EscalatedOutcome"/>.</summary>
/// <param name="ReasonCode">Why, e.g. 'pre-rule', 'model', 'below-threshold' or 'no-provider-decided'.</param>
public abstract record EvaluationOutcome(string ReasonCode)
{
    /// <summary>
    /// The lines the caller prints on stderr, each already starting 'judge: ' (an image the call dropped or refused, with the cause);
    /// empty for a call without images, so its output stays what it was.
    /// </summary>
    public IReadOnlyList<string> StderrLines { get; init; } = [];
}
