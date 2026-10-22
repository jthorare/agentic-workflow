namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>One state's (or one gate's) figures.</summary>
/// <param name="State">The state id, or a gate's block id.</param>
/// <param name="MedianSeconds">The median seconds a run spent in it; null for a gate, or a state no run left.</param>
/// <param name="Parks">Parks in it, by reason.</param>
/// <param name="Escalations">Escalations in it, by reason.</param>
/// <param name="Takeovers">Takeovers of it.</param>
/// <param name="GateRefusals">For a gate: refused and evaluated; null otherwise.</param>
public sealed record StateStats(string State, long? MedianSeconds, IReadOnlyList<ReasonCount> Parks, IReadOnlyList<ReasonCount> Escalations, int Takeovers, GateRefusals? GateRefusals);
