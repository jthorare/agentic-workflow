namespace Aw.Judge.Infrastructure.Providers;

/// <summary>A 'choice' answer: the option picked, its confidence and every option's probability.</summary>
/// <param name="Type">'choice' for the judge's question.</param>
/// <param name="Choice">The option picked.</param>
/// <param name="Confidence">How sure, 0 to 1.</param>
/// <param name="Probabilities">Each option's probability.</param>
internal sealed record JevAnswer(string? Type = null, string? Choice = null, double? Confidence = null, IReadOnlyDictionary<string, double>? Probabilities = null);
