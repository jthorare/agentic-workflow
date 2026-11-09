namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>GET /sindri/workflows/{slug}/runs: a workflow's newest runs, newest first.</summary>
/// <param name="Slug">The workflow.</param>
/// <param name="Runs">The runs; none when it never ran.</param>
/// <param name="More">True when older runs exist past the ones served (the server serves at most 50).</param>
internal sealed record WorkflowRunList(string Slug, IReadOnlyList<WorkflowRunRowView> Runs, bool More = false);
