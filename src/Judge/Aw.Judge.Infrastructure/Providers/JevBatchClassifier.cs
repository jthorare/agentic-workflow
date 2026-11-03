using Aw.Judge.Core.Batching;
using Aw.Judge.Core.Config;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Infrastructure.Providers;

/// <summary>
/// IBatchClassifier over Jev: first the same inclusion test ProviderCatalog.Lineup applies to the chain (the allowlist and
/// 'providers.jev: false'), so an excluded Jev is unavailable ('excluded') without a key read or a request, then the
/// batched call. While sending images to Jev is off (config.json's providers.jevImages, or AW_JUDGE_JEV_IMAGES=0, the default) a
/// batch's images are taken out before anything is sent and the result says so in ImageNotes, as a single question's call does.
/// </summary>
/// <param name="jev">The Jev provider (shared with the catalog, which owns it).</param>
/// <param name="environment">Reads AW_JUDGE_JEV_IMAGES; null reads nothing, so images stay off.</param>
internal sealed class JevBatchClassifier(JevProvider jev, Func<string, string?>? environment = null) : IBatchClassifier
{
    private const string ImagesOff = "image sending to Jev is off (judge config set providers jev-images true); asked jev as text";

    /// <inheritdoc />
    public async ValueTask<ProviderResult> Classify(BatchRequest request, ProvidersConfig? providers, IReadOnlyCollection<ProviderName>? allowed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!JevInclusion.Of(providers, allowed))
        {
            return new ProviderUnavailable(ProviderReasons.Excluded);
        }

        if (request.Images.Count == 0 || JevImagesSetting.IsOn(providers, environment?.Invoke(JevImagesSetting.Variable)))
        {
            return await Send(request, providers, cancellationToken).ConfigureAwait(false);
        }

        var result = await Send(request with { Images = [] }, providers, cancellationToken).ConfigureAwait(false);
        return result with { ImageNotes = [.. result.ImageNotes ?? [], ImagesOff] };
    }

    private async ValueTask<ProviderResult> Send(BatchRequest request, ProvidersConfig? providers, CancellationToken cancellationToken)
    {
        try
        {
            return await jev.Batch(request, providers?.InputCapChars ?? ProvidersConfig.DefaultInputCapChars, cancellationToken).ConfigureAwait(false);
        }
        // The boundary backstop, as FailOpenModelProvider: an adapter bug is an errored result; cancellation propagates.
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new ProviderErrored(ProviderReasons.ProviderThrew);
        }
    }
}
