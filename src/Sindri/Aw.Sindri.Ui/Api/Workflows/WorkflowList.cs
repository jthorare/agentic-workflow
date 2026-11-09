namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>GET /sindri/workflows: the workflows, by slug.</summary>
/// <param name="Workflows">One row per workflow; none when nothing was made yet.</param>
internal sealed record WorkflowList(IReadOnlyList<WorkflowRowView> Workflows);
