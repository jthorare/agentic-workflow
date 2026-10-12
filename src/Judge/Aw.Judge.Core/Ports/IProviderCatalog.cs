using Aw.Judge.Core.Config;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Core.Ports;

/// <summary>Builds the provider chain for this process: the one place the config, AW_PROVIDER and PATH are combined.</summary>
public interface IProviderCatalog
{
    /// <summary>
    /// The lineup: the agent CLIs (config.json's list when it has one, else the default order led by the AW_PROVIDER
    /// host) that are installed, Jev unless config turns it off, and the given rules provider; limited to the allowlist (rules always kept).
    /// </summary>
    /// <param name="config">config.json's providers block, or null for the defaults.</param>
    /// <param name="allowed">The --providers / AW_JUDGE_PROVIDERS allowlist, or null for every provider.</param>
    /// <param name="rules">The rules provider that closes every chain (the engine's own; the catalog doesn't make one).</param>
    /// <returns>The providers and the chain.</returns>
    public ProviderLineup Lineup(ProvidersConfig? config, IReadOnlyCollection<ProviderName>? allowed, IModelProvider rules);
}
