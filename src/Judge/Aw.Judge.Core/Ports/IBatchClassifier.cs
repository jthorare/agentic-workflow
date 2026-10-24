using Aw.Judge.Core.Batching;
using Aw.Judge.Core.Config;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Core.Ports;

/// <summary>
/// Asks Jev several questions about one state in one call (the prompt sorter and trait-check call it; the agent CLIs can't
/// batch). It never throws, except OperationCanceledException for the token.
/// </summary>
public interface IBatchClassifier
{
    /// <summary>
    /// Asks the batch. A Jev that is excluded (the allowlist leaves it out, or config.json says 'providers.jev: false',
    /// judged as <see cref="IProviderCatalog"/> judges them), has no API key, can't be reached or runs past the budget is
    /// <see cref="ProviderUnavailable"/>, never an error: it is what makes trait-check exit 3 and the prompt sorter fall
    /// back to heuristics. An API error status, a body that isn't an answer or a question the API can't take (too many
    /// options or levels) is <see cref="ProviderErrored"/>. Otherwise <see cref="BatchDecided"/>.
    /// </summary>
    /// <param name="request">The questions and the state.</param>
    /// <param name="providers">config.json's providers block, or null for the defaults.</param>
    /// <param name="allowed">The --providers / AW_JUDGE_PROVIDERS allowlist, or null for every provider.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The answers, or why there are none.</returns>
    public ValueTask<ProviderResult> Classify(BatchRequest request, ProvidersConfig? providers, IReadOnlyCollection<ProviderName>? allowed, CancellationToken cancellationToken);
}
