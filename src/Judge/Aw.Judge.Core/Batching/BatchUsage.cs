namespace Aw.Judge.Core.Batching;

/// <summary>What a batch call cost, as the API reports it.</summary>
/// <param name="InputTokens">Tokens read.</param>
/// <param name="OutputTokens">Tokens written.</param>
public sealed record BatchUsage(long InputTokens, long OutputTokens);
