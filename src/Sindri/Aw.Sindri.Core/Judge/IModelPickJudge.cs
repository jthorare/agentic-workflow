namespace Aw.Sindri.Core.Judge;

/// <summary>
/// The judge's model-pick question as a port (deps.yaml Sindri to Judge, kind cli): choose the cheapest candidate that will pass. The judge's chain is Jev, then a rules provider
/// that answers the cheapest key; no agent CLI ever sees the prompt (model-pick spec M5).
/// </summary>
public interface IModelPickJudge
{
    /// <summary>Asks the question.</summary>
    /// <param name="ask">The step, its brief and files, and the candidates.</param>
    /// <param name="cancellationToken">Cancels the call: the judge is killed.</param>
    /// <returns>The answer: <see cref="ChooseAnswer.Choice"/> is a candidate key, <see cref="ChooseAnswer.Provider"/> is 'jev' or 'rules'; a failed one carries the reason, it never throws for the judge's own failures.</returns>
    public ValueTask<ChooseAnswer> Pick(ModelPickAsk ask, CancellationToken cancellationToken);
}
