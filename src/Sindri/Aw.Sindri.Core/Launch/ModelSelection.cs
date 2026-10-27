namespace Aw.Sindri.Core.Launch;

/// <summary>The candidates for one step launch and what was removed on the way (model-pick spec §4.1).</summary>
/// <param name="Candidates">The models left, cheapest first. One means no question is asked; none means SND-MODEL-003.</param>
/// <param name="Removed">Each removed model with the first rule that took it out, in rule order and then cheapest first.</param>
public sealed record ModelSelection(IReadOnlyList<RankedModel> Candidates, IReadOnlyList<ModelRemoval> Removed);
