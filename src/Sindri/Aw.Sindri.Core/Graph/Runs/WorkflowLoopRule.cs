using Aw.Kernel;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Runs;

/// <summary>The loop rule of a manual workflow run (#2258, #2452): a session that a run of a workflow launched can't start that same workflow, so an agent can't make a workflow call itself. Other workflows are fine (depth limits come with PR 9).</summary>
public static class WorkflowLoopRule
{
    /// <summary>Refuses a run of <paramref name="slug"/> when the session asking was launched by a run of that workflow.</summary>
    /// <param name="parent">The run that launched the caller's session, or null when none did.</param>
    /// <param name="slug">The workflow the caller wants to run.</param>
    /// <returns>SND-GRAPH-109 naming the run, or null when the run may start.</returns>
    public static Error? Decide(WorkflowRunId? parent, WorkflowSlug slug) =>
        parent is { } run && run.Value.StartsWith(slug.Value + "@", StringComparison.Ordinal)
            ? SindriErrors.GraphRunLoop.With($"this session was launched by run '{run.Value}' of '{slug.Value}', which can't start '{slug.Value}' again")
            : null;
}
