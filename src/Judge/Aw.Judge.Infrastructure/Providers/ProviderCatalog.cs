using Aw.Judge.Core.Config;
using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using Aw.Kernel;
using ProviderNames = Aw.Judge.Core.Providers.ProviderNames;

namespace Aw.Judge.Infrastructure.Providers;

/// <summary>IProviderCatalog over a fixed set of provider instances, the PATH locator and AW_PROVIDER.</summary>
/// <param name="environment">Reads AW_PROVIDER.</param>
/// <param name="locator">Finds the installed agent CLIs.</param>
/// <param name="providers">One instance of every model provider (jev and each agent CLI); rules comes with each Lineup call.</param>
/// <param name="adjudicator">The claude-cli provider set up for 'judge adjudicate' (opus, high effort), or null when there is none.</param>
internal sealed class ProviderCatalog(Func<string, string?> environment, IExecutableLocator locator, IReadOnlyList<IModelProvider> providers, IModelProvider? adjudicator = null) : IProviderCatalog
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

    /// <inheritdoc />
    public Result<IModelProvider> Provider(ProviderName name, IReadOnlyCollection<ProviderName>? allowed)
    {
        if (allowed is not null && !allowed.Contains(name))
        {
            return JudgeErrors.ProviderExcluded.With($"{ProviderNames.Name(name)} is excluded by the providers allowlist");
        }

        var provider = providers.FirstOrDefault(p => p.Name == name);
        var installed = AgentCliNames.FromProvider(name) is not { } cli || locator.IsOnPath(AgentCliNames.Binary(cli));
        return provider is null || !installed
            ? JudgeErrors.ProviderUnavailable.With($"{ProviderNames.Name(name)} is not available")
            : Results.Success<IModelProvider>(new FailOpenModelProvider(provider));
    }

    /// <inheritdoc />
    public Result<IModelProvider> Adjudicator(IReadOnlyCollection<ProviderName>? allowed)
    {
        if (allowed is not null && !allowed.Contains(ProviderName.ClaudeCli))
        {
            return JudgeErrors.ProviderExcluded.With($"{ProviderNames.Name(ProviderName.ClaudeCli)} is excluded by the providers allowlist");
        }

        return adjudicator is null || !locator.IsOnPath(AgentCliNames.Binary(AgentCliName.ClaudeCli))
            ? JudgeErrors.ProviderUnavailable.With($"{ProviderNames.Name(ProviderName.ClaudeCli)} is not available")
            : Results.Success<IModelProvider>(new FailOpenModelProvider(adjudicator));
    }

    // config.json's names are checked on read (JudgeConfigParser), so an unknown one can't reach here; skip it if it does.
    private static List<AgentCliName>? Configured(IReadOnlyList<string>? names) =>
        names?.Select(name => ProviderNames.TryParse(name, out var provider) ? AgentCliNames.FromProvider(provider) : null)
            .OfType<AgentCliName>()
            .ToList();
}
