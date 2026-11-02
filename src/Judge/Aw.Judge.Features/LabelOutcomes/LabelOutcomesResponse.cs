namespace Aw.Judge.Features.LabelOutcomes;

/// <summary>The LabelOutcomes response.</summary>
/// <param name="Labeled">How many 'ask-check' items got an outcome label.</param>
/// <param name="NoSignal">How many had no usable next prompt (no transcript, no later prompt, or past six hours).</param>
public sealed record LabelOutcomesResponse(int Labeled, int NoSignal);
