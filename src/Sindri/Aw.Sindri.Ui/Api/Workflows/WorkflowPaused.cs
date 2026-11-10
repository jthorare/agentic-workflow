namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>POST /sindri/workflows/{slug}/pause: the workflow is off.</summary>
/// <param name="Slug">The workflow.</param>
/// <param name="LatestRevision">Its newest revision, which stays saved.</param>
internal sealed record WorkflowPaused(string Slug, int LatestRevision);
