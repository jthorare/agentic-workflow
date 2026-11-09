using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Host.Mcp;

/// <summary>The loop guard of 'workflow_run' (#2258, PR 8b): a session that a run of a workflow launched can't start that same workflow, so an agent can't make a workflow call itself. Other workflows are fine (depth limits come with PR 9).</summary>
internal static class WorkflowLoopGuard
{
    /// <summary>Refuses a run of <paramref name="slug"/> by the session of <paramref name="launch"/> when a run of that workflow launched it.</summary>
    /// <param name="launches">Which run launched a session.</param>
    /// <param name="launch">The caller's launch, or null for a person.</param>
    /// <param name="slug">The workflow the caller wants to run.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>SND-GRAPH-109 naming the run, the read's error, or null when the run may start.</returns>
    internal static async ValueTask<Error?> Check(IGraphLaunchRuns launches, LaunchId? launch, WorkflowSlug slug, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(launches);
        if (launch is not { } id)
        {
            return null;
        }

        var run = await launches.RunOf(id, cancellationToken).ConfigureAwait(false);
        if (run.IsError)
        {
            return run.Error;
        }

        return run.Value is { } parent && parent.Value.StartsWith(slug.Value + "@", StringComparison.Ordinal)
            ? SindriErrors.GraphRunLoop.With($"this session was launched by run '{parent.Value}' of '{slug.Value}', which can't start '{slug.Value}' again")
            : null;
    }
}
