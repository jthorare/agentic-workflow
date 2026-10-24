namespace Aw.Judge.Infrastructure.Providers;

/// <summary>An answer of a batched response: 'type' says which of the other fields is set.</summary>
/// <param name="Type">'choice', 'noul' or 'score'.</param>
/// <param name="Choice">The option picked ('choice').</param>
/// <param name="Noul">The number ('noul').</param>
/// <param name="Score">The level ('score').</param>
/// <param name="Confidence">How sure, 0 to 1.</param>
/// <param name="Probabilities">Each option's or level's probability.</param>
internal sealed record JevBatchAnswer(
    string? Type = null,
    string? Choice = null,
    double? Noul = null,
    double? Score = null,
    double? Confidence = null,
    IReadOnlyDictionary<string, double>? Probabilities = null);
