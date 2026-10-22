namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>A park or escalation reason and how often it happened (GET /workflow/recipes/{id}/stats).</summary>
/// <param name="Reason">The reason as served, such as no-progress.</param>
/// <param name="N">How many times.</param>
internal sealed record ReasonCountView(string Reason, int N);
