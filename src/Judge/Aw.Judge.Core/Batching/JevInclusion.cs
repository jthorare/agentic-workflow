using Aw.Judge.Core.Config;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Core.Batching;

/// <summary>Whether Jev is in the chain, judged as 'IProviderCatalog.Lineup' judges it (so a batch and a question agree).</summary>
public static class JevInclusion
{
    /// <summary>
    /// Builds the chain the catalog would build (Jev unless config.json says 'providers.jev: false') and cuts it to the
    /// allowlist; Jev is included when it is still in it.
    /// </summary>
    /// <param name="providers">config.json's providers block, or null for the defaults.</param>
    /// <param name="allowed">The --providers / AW_JUDGE_PROVIDERS allowlist, or null for every provider.</param>
    /// <returns>Whether a batch may go to Jev.</returns>
    public static bool Of(ProvidersConfig? providers, IReadOnlyCollection<ProviderName>? allowed)
    {
        var chain = ChainSpec.Build([], jev: providers?.Jev != false);
        if (allowed is not null)
        {
            chain = chain.Restrict(allowed);
        }

        return chain.Classes.Values.Any(names => names.Contains(ProviderName.Jev));
    }
}
