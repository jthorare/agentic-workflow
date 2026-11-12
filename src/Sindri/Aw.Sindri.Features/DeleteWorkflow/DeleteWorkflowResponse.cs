using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.DeleteWorkflow;

/// <summary>The workflow is archived and its resources are purged.</summary>
/// <param name="Slug">The workflow.</param>
/// <param name="ArchivedAt">When it was archived.</param>
/// <param name="RunsKept">How many run rows the workflow has (test runs included); they stay readable.</param>
/// <param name="BytesFreed">The bytes of the resources that were purged.</param>
/// <param name="ResourcesStayed">The owners (<c lang="text">workflow:slug</c>, <c lang="text">node:slug/id</c>) whose purge failed and whose folders stay; empty when all went.</param>
internal sealed record DeleteWorkflowResponse(WorkflowSlug Slug, DateTimeOffset ArchivedAt, int RunsKept, long BytesFreed, IReadOnlyList<string> ResourcesStayed);
