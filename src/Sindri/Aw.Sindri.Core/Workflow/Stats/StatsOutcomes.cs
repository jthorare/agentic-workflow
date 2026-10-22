namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>The done runs by how much human they needed.</summary>
/// <param name="DoneWithoutHuman">Done with no park answered, escalation decided or takeover.</param>
/// <param name="DoneAfterPark">Done after a park was answered or an escalation decided.</param>
/// <param name="DoneAfterTakeover">Done after a takeover.</param>
public sealed record StatsOutcomes(int DoneWithoutHuman, int DoneAfterPark, int DoneAfterTakeover);
