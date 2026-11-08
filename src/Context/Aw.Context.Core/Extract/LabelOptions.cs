namespace Aw.Context.Core.Extract;

/// <summary>What a labeling pass is asked to do beyond naming (spec §6.1): from 'context.models.labeling' of the profile in hand. The Host port still checks the approved profile before it sends anything to Jev.</summary>
/// <param name="Pick">True when the profile says 'pick: jev': the model writes <paramref name="Candidates"/> labels per cluster and Jev picks.</param>
/// <param name="Candidates">How many candidate labels the model writes per cluster when <paramref name="Pick"/> is true (2 to 5); a pass without the pick asks for one.</param>
public sealed record LabelOptions(bool Pick, int Candidates);
