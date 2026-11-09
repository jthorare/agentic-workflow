namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>POST /sindri/workflows/{slug}/activate: the workflow is on.</summary>
/// <param name="Slug">The workflow.</param>
/// <param name="Revision">The revision its triggers run.</param>
/// <param name="SpecHash">That revision's hash.</param>
internal sealed record WorkflowActivated(string Slug, int Revision, string SpecHash);
