namespace Aw.Context.Core.Eval.Backtest;

/// <summary>A ranker arm's file scores over only the pin-anchored questions: those whose anchor sits in exactly one pin, the questions the pin leg runs for (concept pins spec §6 Q6).</summary>
/// <param name="Count">How many questions were pin-anchored; under <see cref="BacktestScoring.MinPinAnchored"/> the hand check decides.</param>
/// <param name="RecallAt10">Mean share of the truth files in the first 10 predicted files.</param>
/// <param name="Mrr">Mean reciprocal rank of the first truth file in the predicted files (0 when none was found).</param>
public sealed record PinAnchoredScore(int Count, double RecallAt10, double Mrr);
