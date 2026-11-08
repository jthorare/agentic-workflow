namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>Jev's answer to one question: 'Type' says which of the value fields is set.</summary>
/// <param name="Type">'choice', 'score' or 'noul'.</param>
/// <param name="Choice">The option picked ('choice').</param>
/// <param name="Score">The level ('score').</param>
/// <param name="Noul">The number ('noul').</param>
/// <param name="Confidence">How sure, 0 to 1.</param>
/// <param name="Probabilities">Each option's or level's probability.</param>
public sealed record JevAnswerValue(string Type, string? Choice, double? Score, double? Noul, double? Confidence, IReadOnlyDictionary<string, double>? Probabilities);
