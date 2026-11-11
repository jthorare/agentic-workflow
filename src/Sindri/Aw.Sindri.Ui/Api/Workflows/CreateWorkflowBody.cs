namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>The body of POST /sindri/workflows when the workflow starts from a template. The spec is left out: Sindri takes a template or a spec, never both.</summary>
/// <param name="Slug">The new workflow's slug.</param>
/// <param name="Template">The starter template's name.</param>
internal sealed record CreateWorkflowBody(string Slug, string Template);
