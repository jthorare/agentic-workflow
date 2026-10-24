namespace Aw.Judge.Core.Batching;

/// <summary>The level picked.</summary>
/// <param name="Score">The level's position on the scale the model reports.</param>
/// <param name="Confidence">How sure, 0 to 1 (1 when the model gave none).</param>
/// <param name="Probabilities">Each level's probability; empty when the model gave none.</param>
public sealed record ScoreBatchAnswer(double Score, double Confidence, IReadOnlyDictionary<string, double> Probabilities) : BatchAnswer;
