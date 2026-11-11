namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>POST /sindri/workflows: the new workflow, off, at its first revision.</summary>
/// <param name="Id">The workflow's id.</param>
/// <param name="Slug">The workflow.</param>
/// <param name="Revision">Its first revision.</param>
/// <param name="SpecHash">The hash of its graph.</param>
internal sealed record WorkflowCreated(string Id, string Slug, int Revision, string SpecHash);
