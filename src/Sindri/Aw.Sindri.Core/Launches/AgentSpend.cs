namespace Aw.Sindri.Core.Launches;

/// <summary>What an agent has used in a period (screens spec E-28).</summary>
/// <param name="Usd">The priced part in USD: models the price table lacks add nothing here.</param>
/// <param name="Tokens">Every token used, priced or not.</param>
/// <param name="Basis"><see cref="SpendBasis.TokensOnly"/> when any model was unpriced.</param>
public sealed record AgentSpend(decimal Usd, long Tokens, SpendBasis Basis);
