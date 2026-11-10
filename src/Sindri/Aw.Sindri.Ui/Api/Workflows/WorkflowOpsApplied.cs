namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>POST /sindri/workflows/{slug}/ops: the revision the ops made.</summary>
/// <param name="Slug">The workflow.</param>
/// <param name="Revision">The new revision.</param>
/// <param name="SpecHash">Its hash.</param>
internal sealed record WorkflowOpsApplied(string Slug, int Revision, string SpecHash);
