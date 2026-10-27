namespace Aw.Judge.Features.EvaluateSort;

/// <summary>A sorter policy's score on one axis.</summary>
/// <param name="Accuracy">Correct over all scored items.</param>
/// <param name="Ppv">Positive predictive value, or null when the policy never said yes.</param>
public sealed record PolicyScore(double Accuracy, double? Ppv);
