namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>One step's figures in a recipe's stats (GET /workflow/recipes/{id}/stats).</summary>
/// <param name="State">The step's id as served.</param>
/// <param name="MedianSeconds">The typical time in the step in seconds; null when no run passed it.</param>
/// <param name="Parks">Why runs parked here, by reason.</param>
/// <param name="Escalations">Why runs escalated here, by reason.</param>
/// <param name="Takeovers">Runs a person took over here.</param>
/// <param name="GateRefusals">How often the step's gate refused; null when the step has no gate.</param>
internal sealed record StateStatsView(string State, long? MedianSeconds, IReadOnlyList<ReasonCountView> Parks, IReadOnlyList<ReasonCountView> Escalations, int Takeovers, GateRefusalsView? GateRefusals);
