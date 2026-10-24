namespace Aw.Judge.Core.Batching;

/// <summary>The option picked, one of the question's criteria.</summary>
/// <param name="Choice">The option.</param>
/// <param name="Confidence">How sure, 0 to 1 (1 when the model gave none).</param>
/// <param name="Probabilities">Each option's probability; empty when the model gave none.</param>
public sealed record ChoiceBatchAnswer(string Choice, double Confidence, IReadOnlyDictionary<string, double> Probabilities) : BatchAnswer;
