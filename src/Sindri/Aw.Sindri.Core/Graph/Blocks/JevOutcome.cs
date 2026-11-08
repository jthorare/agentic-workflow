namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>What Jev answered.</summary>
/// <param name="Model">The model that answered.</param>
/// <param name="Answers">The answers by question name.</param>
/// <param name="InputTokens">Tokens read, or null when the API did not say.</param>
/// <param name="OutputTokens">Tokens written, or null when the API did not say.</param>
public sealed record JevOutcome(string Model, IReadOnlyDictionary<string, JevAnswerValue> Answers, double? InputTokens, double? OutputTokens);
