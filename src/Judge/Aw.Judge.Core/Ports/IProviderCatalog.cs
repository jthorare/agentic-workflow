using Aw.Judge.Core.Config;
using Aw.Judge.Core.Providers;
using Aw.Kernel;

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

    /// <summary>
    /// One provider by name, called directly with no chain around it ('judge eval --provider', which scores a provider
    /// against the labels). An agent CLI must be installed; the allowlist is honoured.
    /// </summary>
    /// <param name="name">The provider.</param>
    /// <param name="allowed">The --providers / AW_JUDGE_PROVIDERS allowlist, or null for every provider.</param>
    /// <returns>The provider, AW-JUDGE-012 when the allowlist excludes it, or AW-JUDGE-013 when it can't be asked (its CLI is missing, or it is rules).</returns>
    public Result<IModelProvider> Provider(ProviderName name, IReadOnlyCollection<ProviderName>? allowed);

    /// <summary>
    /// The stronger model 'judge adjudicate' asks: the claude CLI on opus at high effort, in a locked-down child (no tools,
    /// settings or hooks; a scratch directory; a minimal environment). Same refusals as <see cref="Provider"/> for claude-cli.
    /// </summary>
    /// <param name="allowed">The --providers / AW_JUDGE_PROVIDERS allowlist, or null for every provider.</param>
    /// <returns>The provider, AW-JUDGE-012 when the allowlist excludes claude-cli, or AW-JUDGE-013 when the CLI is missing.</returns>
    public Result<IModelProvider> Adjudicator(IReadOnlyCollection<ProviderName>? allowed);
}
