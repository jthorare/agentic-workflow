using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.GetWorkflowRun;

/// <summary>One execution of one node.</summary>
/// <param name="Node">The node.</param>
/// <param name="ItemIndex">The item's position, 0 for a node that runs once.</param>
/// <param name="Status">'running', 'waiting', 'succeeded', 'failed', 'skipped' or 'cancelled'.</param>
/// <param name="Input">The interpolated config the block got, or null.</param>
/// <param name="Output">What the block produced, or null.</param>
/// <param name="Truncated">True when Sindri cut the input or the output to fit its size limit.</param>
/// <param name="Error">Why it failed, or null.</param>
/// <param name="Agent">Null unless the row is a 'run_agent' node the run waits on; then 'waiting' (a takeover applies) or 'taken_over' (a handback applies).</param>
/// <param name="Raw">The scrubbed model text of a failed prompt step, cut at 64 KB, or null (P3e, spec §6.2).</param>
internal sealed record NodeRunView(NodeId Node, int ItemIndex, string Status, JsonNode? Input, JsonNode? Output, bool Truncated, string? Error, string? Agent = null, string? Raw = null)
{
    /// <summary>How long the node ran in seconds; null while it has not ended or when the row has no times. Issue 3683.</summary>
    public double? Seconds { get; init; }
}
