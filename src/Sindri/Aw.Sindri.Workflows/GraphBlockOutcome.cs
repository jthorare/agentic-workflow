using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What one block execution gave the walk; a block that fails is an outcome, not an activity failure, so the workflow applies the node's 'onFailure'.</summary>
/// <param name="Status">Succeeded or Failed.</param>
/// <param name="Input">The interpolated config as JSON.</param>
/// <param name="Output">The block's outputs as JSON, or what a failed block left through BlockContext.LeaveFailureOutput, or null.</param>
/// <param name="Taken">The outputs an edge may leave by.</param>
/// <param name="Error">Why it failed, or null.</param>
/// <param name="Raw">The scrubbed raw model text a failed prompt step left through BlockContext.LeaveRaw, or null.</param>
internal sealed record GraphBlockOutcome(NodeRunStatus Status, string? Input, string? Output, IReadOnlyList<string> Taken, string? Error, string? Raw = null);
