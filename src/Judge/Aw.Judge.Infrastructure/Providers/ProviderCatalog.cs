using Aw.Judge.Core.Config;
using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using ProviderNames = Aw.Judge.Core.Providers.ProviderNames;

namespace Aw.Judge.Infrastructure.Providers;

/// <summary>IProviderCatalog over a fixed set of provider instances, the PATH locator and AW_PROVIDER.</summary>
/// <param name="environment">Reads AW_PROVIDER.</param>
/// <param name="locator">Finds the installed agent CLIs.</param>
/// <param name="providers">One instance of every model provider (jev and each agent CLI); rules comes with each Lineup call.</param>
internal sealed class ProviderCatalog(Func<string, string?> environment, IExecutableLocator locator, IReadOnlyList<IModelProvider> providers) : IProviderCatalog
{
    /// <inheritdoc />
    public ProviderLineup Lineup(ProvidersConfig? config, IReadOnlyCollection<ProviderName>? allowed, IModelProvider rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (rules.Name != ProviderName.Rules)
        {
            throw new ArgumentException($"expected the rules provider, got {ProviderNames.Name(rules.Name)}", nameof(rules));
        }

        var clis = AgentCliResolution.Resolve(locator, environment("AW_PROVIDER"), Configured(config?.AgentClis));
        var chain = ChainSpec.Build(clis, jev: config?.Jev != false);
        if (allowed is not null)
        {
            chain = chain.Restrict(allowed);
        }

        var named = chain.Classes.Values.SelectMany(names => names).ToHashSet();
        return new ProviderLineup([.. providers.Where(p => named.Contains(p.Name)).Select(p => new FailOpenModelProvider(p)), rules], chain);
    }

    // config.json's names are checked on read (JudgeConfigParser), so an unknown one can't reach here; skip it if it does.
    private static List<AgentCliName>? Configured(IReadOnlyList<string>? names) =>
        names?.Select(name => ProviderNames.TryParse(name, out var provider) ? AgentCliNames.FromProvider(provider) : null)
            .OfType<AgentCliName>()
            .ToList();
}
