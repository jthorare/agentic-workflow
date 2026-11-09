using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Guards;

/// <summary>
/// The loop guards of a workflow that starts a workflow: a call may be at most 'workflow.callDepth' (default 3) deep, and a run may not start its own workflow.
/// A top-level run is depth 0, the run it calls is 1, and so on, so depth 3 is allowed and 4 is refused. The depth and the caller travel in the callee's trigger as
/// 'callDepth' and 'parentRun'.
/// </summary>
public static class CallDepth
{
    /// <summary>The depth 'workflow.callDepth' allows when the profile says nothing.</summary>
    public const int DefaultMax = 3;

    /// <summary>Decides whether a run may start <paramref name="target"/>.</summary>
    /// <param name="caller">The calling run.</param>
    /// <param name="target">The slug of the workflow to start.</param>
    /// <param name="max">The deepest call allowed.</param>
    /// <param name="launchLineage">
    /// The slugs of the workflows the calling run's launch descends from, or null. Seam for PR 7: a session's own writes carry its run, so a session that starts a workflow
    /// can be traced back to the workflow that launched it; until then only the trigger's 'parentRun' is read.
    /// </param>
    /// <returns>The callee's depth, SND-GRAPH-121 when the run would start its own workflow, or SND-GRAPH-120 when the call is too deep.</returns>
    public static Result<int> Next(CallOrigin caller, string target, int max, IReadOnlyCollection<string>? launchLineage = null) =>
        throw new NotSupportedException("CallDepth.Next is not written yet");

    /// <summary>The slug part of a run id, '&lt;slug&gt;@&lt;revision&gt;:&lt;occurrence&gt;'.</summary>
    /// <param name="run">The run id text.</param>
    /// <returns>The slug.</returns>
    public static string SlugOf(string run) => throw new NotSupportedException("CallDepth.SlugOf is not written yet");
}
