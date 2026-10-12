using Aw.Judge.Core.Providers;

namespace Aw.Judge.Core.Ports;

/// <summary>The providers this process runs with and the chain that orders them (an EvaluationContext's Providers and Chain).</summary>
/// <param name="Providers">One instance per provider the chain names; rules always among them.</param>
/// <param name="Chain">The routing per content class; every name in it is in <paramref name="Providers"/>.</param>
public sealed record ProviderLineup(IReadOnlyList<IModelProvider> Providers, ChainSpec Chain);
