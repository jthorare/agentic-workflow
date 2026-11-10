using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// What 'CloseAgentNode' does (PR 7): reads the worktree's HEAD and the agent's own session id (when asked to measure), then lets <see cref="AgentClosing"/> take the final usage sample, read the usage and
/// the diff signal and stop the session. The HEAD is read first: it is part of the node's outputs, and the stop must not come before it.
/// </summary>
/// <param name="closing">Measures and stops.</param>
/// <param name="diff">Reads the HEAD.</param>
/// <param name="views">The 'launches' view, which holds the agent's own session id.</param>
internal sealed class AgentNodeClosing(AgentClosing closing, IRunDiff diff, ILaunchViews views)
{
    /// <summary>Closes the node run's session.</summary>
    /// <param name="run">The launch and what to measure.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The measures.</returns>
    public async Task<AgentNodeClosed> Close(AgentCloseRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        string? commit = null;
        string? session = null;
        if (run.Measure)
        {
            if (run.Worktree is { } worktree && await diff.Head(worktree, cancellationToken).ConfigureAwait(false) is { IsError: false } head)
            {
                commit = head.Value;
            }

            if (await views.Get(LaunchId.From(run.Launch), cancellationToken).ConfigureAwait(false) is { IsError: false, Value: { } row })
            {
                session = row.ResumableId?.Value;
            }
        }

        var closed = await closing.Close(run, cancellationToken).ConfigureAwait(false);
        return new AgentNodeClosed(closed.Tokens, closed.EmptyDiff, commit, session);
    }
}
