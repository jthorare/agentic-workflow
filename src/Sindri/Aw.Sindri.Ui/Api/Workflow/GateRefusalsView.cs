namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>How often a step's gate said no (GET /workflow/recipes/{id}/stats).</summary>
/// <param name="Refused">Times the gate refused.</param>
/// <param name="Evaluated">Times the gate was asked.</param>
internal sealed record GateRefusalsView(int Refused, int Evaluated);
