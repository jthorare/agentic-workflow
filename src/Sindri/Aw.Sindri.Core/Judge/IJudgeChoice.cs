namespace Aw.Sindri.Core.Judge;

/// <summary>
/// The judge's choose question as a port (deps.yaml Sindri to Judge, kind cli): one question with a fixed set of options, answered with
/// one of them or an escalation. Every call writes a decision row in the judge's own store, escalations included.
/// </summary>
public interface IJudgeChoice
{
    /// <summary>Asks the question.</summary>
    /// <param name="request">The question, its options and facts, the threshold and the providers allowed.</param>
    /// <param name="cancellationToken">Cancels the call: the judge is killed.</param>
    /// <returns>The answer; a failed one carries the reason, it never throws for the judge's own failures.</returns>
    public ValueTask<ChooseAnswer> Choose(ChooseRequest request, CancellationToken cancellationToken);
}
