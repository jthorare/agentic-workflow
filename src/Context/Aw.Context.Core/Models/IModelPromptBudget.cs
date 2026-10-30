namespace Aw.Context.Core.Models;

/// <summary>Tells a caller how big a prompt it may send for a purpose, before the router picks a harness (spec §6.2): the router can fall back mid-call, so a pack must be sized for the smallest harness it could land on.</summary>
public interface IModelPromptBudget
{
    /// <summary>The smallest prompt budget among the installed harnesses the purpose's route would try.</summary>
    /// <param name="purpose">The purpose of the call.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The budget, or null when no harness is installed (the call will fail on its own).</returns>
    public ValueTask<PromptBudget?> For(ModelPurpose purpose, CancellationToken cancellationToken);
}
