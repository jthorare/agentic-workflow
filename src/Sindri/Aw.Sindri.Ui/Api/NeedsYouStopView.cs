namespace Aw.Sindri.Ui.Api;

/// <summary>Where a desk row sits among the desk states of its recipe, as served ("Approval n of of").</summary>
/// <param name="N">The 1-based position of the desk state the instance is parked in.</param>
/// <param name="Of">How many desk states the recipe has.</param>
internal sealed record NeedsYouStopView(int N, int Of);
