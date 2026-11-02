namespace Aw.Sindri.Features.GetLaunchPicks;

/// <summary>The answer of GET /sindri/launch/picks.</summary>
/// <param name="Picks">One row per run that has a 'model.picking' or a 'model.picked', oldest first.</param>
/// <param name="Escalation">Per state and model of the picks, how often it was retried on a stronger model over its last 20 runs; a state with no history is left out.</param>
public sealed record LaunchPicks(IReadOnlyList<LaunchPickView> Picks, IReadOnlyList<StateEscalation>? Escalation = null);
