namespace Aw.Context.Core.Eval.Backtest;

/// <summary>One arm's row in a report.</summary>
/// <param name="Arm">The arm's name.</param>
/// <param name="Dimension">'source' for an arm that varies the inputs, 'ranker' for one that varies the ranker stages over the same inputs (S-8).</param>
/// <param name="Leaky">True for the ceiling arm: it reads the current map, which saw decisions after the cut.</param>
/// <param name="NotAvailable">For an arm that doesn't run yet: the stage that brings it ('S-6', 'S-7B', 'S-9'); else null.</param>
/// <param name="Score">The arm's means; null when it did not run.</param>
/// <param name="PassLine">The arm against the §9.4 bars; null when it did not run.</param>
/// <param name="Samples">Each sample's score on this arm.</param>
public sealed record ArmReport(string Arm, string Dimension, bool Leaky, string? NotAvailable, ArmScore? Score, ArmPassLine? PassLine, IReadOnlyList<SampleScore> Samples);
