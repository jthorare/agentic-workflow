namespace Aw.Sindri.Ui.Api;

/// <summary>GET /sindri/launch/picks (model-pick spec §6.1): the model picks of an item's or an instance's runs, oldest first.</summary>
/// <param name="Picks">One row per run that has a pick.</param>
/// <param name="Escalation">Per state and model, how often it was retried on a stronger model; a state with no history is left out. Null from an older server.</param>
internal sealed record LaunchPicksView(IReadOnlyList<LaunchPickRow> Picks, IReadOnlyList<StateEscalationView>? Escalation = null);
