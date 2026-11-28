using System.Text.Json.Nodes;
using Aw.Judge.Core.Batching;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Core.Ports;

/// <summary>Asks Jev the trait questions about one excerpt: the state redacted and the call made with config.json's providers block.</summary>
public interface ITraitJudge
{
    /// <summary>
    /// Asks one batch. Like <see cref="IBatchClassifier.Classify"/> it never throws, except OperationCanceledException: a Jev
    /// that is excluded, has no key, can't be reached or runs out of budget is <see cref="ProviderUnavailable"/>.
    /// </summary>
    /// <param name="state">What the questions are about (a diff excerpt); redacted before it leaves the machine.</param>
    /// <param name="questions">The questions, one per trait.</param>
    /// <param name="budget">The time budget for the call.</param>
    /// <param name="allowed">The --providers / AW_JUDGE_PROVIDERS allowlist, or null for every provider.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The answers, or why there are none.</returns>
    public ValueTask<ProviderResult> Ask(JsonNode? state, IReadOnlyList<BatchQuestion> questions, TimeSpan budget, IReadOnlyCollection<ProviderName>? allowed, CancellationToken cancellationToken);

    /// <summary>
    /// Asks one batch that may carry images (an edited PNG or JPEG), best effort: one Jev can't take is dropped, and the result's ImageNotes
    /// says so, so the caller never judges the file on its name alone. The request's state is redacted before it leaves the machine.
    /// </summary>
    /// <param name="request">The state, questions, budget and images.</param>
    /// <param name="allowed">The --providers / AW_JUDGE_PROVIDERS allowlist, or null for every provider.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The answers, or why there are none.</returns>
    public ValueTask<ProviderResult> Ask(BatchRequest request, IReadOnlyCollection<ProviderName>? allowed, CancellationToken cancellationToken);

    /// <summary>
    /// Whether a batch may go to Jev at all (config.json's 'providers.jev' and the allowlist, as <see cref="Ask(BatchRequest, IReadOnlyCollection{ProviderName}?, CancellationToken)"/> judges it).
    /// A caller that reuses Jev's cached verdicts reuses them only when Jev is included, so excluding Jev still judges nothing.
    /// </summary>
    /// <param name="allowed">The --providers / AW_JUDGE_PROVIDERS allowlist, or null for every provider.</param>
    /// <param name="cancellationToken">Cancels the config read.</param>
    /// <returns>Whether Jev is in the chain.</returns>
    public ValueTask<bool> Included(IReadOnlyCollection<ProviderName>? allowed, CancellationToken cancellationToken);
}
