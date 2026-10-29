using Aw.Judge.Core.Config;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using Aw.Kernel;

namespace Aw.Sindri.Tests.ContextCalls.Routing;

/// <summary>A lineup of the CLIs given, in the order given (the order the chain tries them).</summary>
internal sealed class ScriptedCatalog(params ScriptedCli[] clis) : IProviderCatalog
{
    public IReadOnlyCollection<ProviderName>? Allowed { get; private set; }

    public ProviderLineup Lineup(ProvidersConfig? config, IReadOnlyCollection<ProviderName>? allowed, IModelProvider rules)
    {
        Allowed = allowed;
        var chain = ChainSpec.Build([.. clis.Select(static c => AgentCliNames.FromProvider(c.Name)!.Value)], jev: false);
        return new ProviderLineup([.. clis, rules], chain);
    }

    Result<IModelProvider> IProviderCatalog.Provider(ProviderName name, IReadOnlyCollection<ProviderName>? allowed) =>
        throw new NotSupportedException("the model port asks for a lineup, never one provider by name");

    Result<IModelProvider> IProviderCatalog.Adjudicator(IReadOnlyCollection<ProviderName>? allowed) =>
        throw new NotSupportedException("the model port asks for a lineup, never the adjudicator");
}
