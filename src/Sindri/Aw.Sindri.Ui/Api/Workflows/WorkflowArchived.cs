namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>DELETE /sindri/workflows/{slug}: the workflow is archived (the app never sends it yet; the golden is read so the shape is checked).</summary>
/// <param name="Slug">The workflow.</param>
/// <param name="ArchivedAt">When it was archived.</param>
internal sealed record WorkflowArchived(string Slug, DateTimeOffset ArchivedAt);
