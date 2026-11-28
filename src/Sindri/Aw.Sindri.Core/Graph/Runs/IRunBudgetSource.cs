namespace Aw.Sindri.Core.Graph.Runs;

/// <summary>Where a run reads the token budget its spec leaves out (#3805): the live profile's 'workflow.budget.tokens' in the app.</summary>
public interface IRunBudgetSource
{
    /// <summary>The default budget now.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The tokens, or null when the profile sets none (or can't be read): no cap.</returns>
    public ValueTask<long?> DefaultTokens(CancellationToken cancellationToken);
}
