namespace Aw.Judge.Core.Evals;

/// <summary>One row of an eval report's confidence sweep.</summary>
/// <param name="Threshold">The confidence threshold.</param>
/// <param name="Coverage">The share of items decided at or above it.</param>
/// <param name="AccuracyCovered">The accuracy on those items.</param>
public sealed record SweepRow(double Threshold, double Coverage, double AccuracyCovered);
