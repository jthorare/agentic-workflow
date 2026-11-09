namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>GET /sindri/workflows/{slug}/runs: a workflow's newest runs, newest first.</summary>
/// <param name="Slug">The workflow.</param>
/// <param name="Runs">The runs; none when it never ran.</param>
internal sealed record WorkflowRunList(string Slug, IReadOnlyList<WorkflowRunRowView> Runs);
