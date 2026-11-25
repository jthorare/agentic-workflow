namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The step a new step follows, as its hint names it (#4330): its id and its block's output fields, in the order the block list serves them.</summary>
/// <param name="Id">The step's id.</param>
/// <param name="Outputs">Its block's output fields; empty when the block has none or is unknown.</param>
internal sealed record HintStep(string Id, IReadOnlyList<string> Outputs);
