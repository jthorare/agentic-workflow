using Aw.Judge.Core.Batching;
using Aw.Judge.Core.Config;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Infrastructure.Providers;

/// <summary>
/// IBatchClassifier over Jev: first the same inclusion test ProviderCatalog.Lineup applies to the chain (the allowlist and
/// 'providers.jev: false'), so an excluded Jev is unavailable ('excluded') without a key read or a request, then the
/// batched call.
/// </summary>
/// <param name="jev">The Jev provider (shared with the catalog, which owns it).</param>
internal sealed class JevBatchClassifier(JevProvider jev) : IBatchClassifier
{
    /// <inheritdoc />
    public async ValueTask<ProviderResult> Classify(BatchRequest request, ProvidersConfig? providers, IReadOnlyCollection<ProviderName>? allowed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!JevInclusion.Of(providers, allowed))
        {
            return new ProviderUnavailable(ProviderReasons.Excluded);
        }

        try
        {
            return await jev.Batch(request, cancellationToken).ConfigureAwait(false);
        }
        // The boundary backstop, as FailOpenModelProvider: an adapter bug is an errored result; cancellation propagates.
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new ProviderErrored(ProviderReasons.ProviderThrew);
        }
    }
}
