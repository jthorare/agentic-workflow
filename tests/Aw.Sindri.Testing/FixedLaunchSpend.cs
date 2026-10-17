using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;

namespace Aw.Sindri.Testing;

/// <summary>A spend reader with set figures: an agent not set has no usage reader.</summary>
public sealed class FixedLaunchSpend : ILaunchSpend
{
    private readonly Dictionary<AgentId, AgentSpend> _spent = [];

    /// <summary>Sets what <paramref name="target"/> has spent today.</summary>
    /// <param name="target">The agent.</param>
    /// <param name="usd">The amount in USD.</param>
    public void Spent(AgentId target, decimal usd) => _spent[target] = new AgentSpend(usd, Tokens: 0, SpendBasis.PriceTable);

    /// <summary>Sets what <paramref name="target"/> has used when a model the price table lacks was among it: tokens only.</summary>
    /// <param name="target">The agent.</param>
    /// <param name="tokens">The tokens used.</param>
    public void TokensOnly(AgentId target, long tokens) => _spent[target] = new AgentSpend(0m, tokens, SpendBasis.TokensOnly);

    /// <inheritdoc/>
    public ValueTask<Result<AgentSpend?>> Today(AgentId target, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success(_spent.TryGetValue(target, out var spend) ? spend : null));
}
