using System.Globalization;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.DeleteWorkflow;

/// <summary>The workflow is archived and its resources are purged, as far as the purge got.</summary>
/// <param name="Slug">The workflow.</param>
/// <param name="ArchivedAt">When it was archived.</param>
/// <param name="RunsKept">How many runs the workflow has, test runs included; they stay readable.</param>
/// <param name="BytesFreed">The bytes the purged resources held (an unmeasured resource adds nothing).</param>
/// <param name="ResourcesStayed">The owners (<c lang="text">workflow:slug</c>, <c lang="text">node:slug/id</c>) whose purge failed, so their resources stay; empty when all went.</param>
internal sealed record DeleteWorkflowResponse(WorkflowSlug Slug, DateTimeOffset ArchivedAt, int RunsKept, long BytesFreed, IReadOnlyList<string> ResourcesStayed)
{
    /// <summary>The line 'sindri wf delete' and MCP workflow_delete print: "Deleted nightly-size. 12 runs kept, 1.2 MB freed" (1 MB is 1,000,000 bytes), then the owners that stayed, if any.</summary>
    /// <returns>The line, without a line break.</returns>
    public string Sentence()
    {
        var runs = RunsKept == 1 ? "1 run" : RunsKept.ToString(CultureInfo.InvariantCulture) + " runs";
        var line = string.Create(CultureInfo.InvariantCulture, $"Deleted {Slug.Value}. {runs} kept, {BytesFreed / 1_000_000.0:0.0} MB freed");
        return ResourcesStayed.Count == 0
            ? line
            : line + ". These resources stayed: " + string.Join(", ", ResourcesStayed) + "; run 'sindri resources purge <owner>' to try again";
    }
}
