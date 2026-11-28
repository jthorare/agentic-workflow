namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>DELETE /sindri/workflows/{slug}: the workflow is archived and its resources are purged (the app never sends it yet; the golden is read so the shape is checked).</summary>
/// <param name="Slug">The workflow.</param>
/// <param name="ArchivedAt">When it was archived.</param>
/// <param name="RunsKept">How many runs stay readable, test runs included.</param>
/// <param name="BytesFreed">The bytes the purged resources held.</param>
/// <param name="ResourcesStayed">The owners whose resources could not be deleted; empty when all went.</param>
internal sealed record WorkflowArchived(string Slug, DateTimeOffset ArchivedAt, int RunsKept, long BytesFreed, IReadOnlyList<string> ResourcesStayed);
