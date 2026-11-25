using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Stops a retried launch from starting a second agent for one node run: when the launch id already started a session ('launch.started' in the launches view) and that session has ended
/// ('session.ended'), the launcher can't tell (its running check finds no session), so this answers with the first start and the node's watch then reports the end. Neither fact is deleted when the
/// session ends, so the answer holds after the host session and the per-launch files are gone. The start commit is not kept, so the node's diff goes unmeasured.
/// </summary>
/// <param name="views">The launches view.</param>
/// <param name="sessions">Whether the session ended.</param>
/// <param name="home">Sindri's home, for the run directory.</param>
/// <param name="ledger">The ledger, read directly so the answer doesn't wait for the projector.</param>
internal sealed class AgentLaunchEnded(ILaunchViews views, IAgentSessions sessions, ISindriHome home, ILedgerReader ledger)
{
    /// <summary>Answers for a launch whose session already ended.</summary>
    /// <param name="request">The launch.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The first start's answer, or null when the launch never started or its session still runs.</returns>
    public async Task<AgentLaunched?> Find(LaunchRequest request, CancellationToken cancellationToken)
    {
        var row = await views.Get(request.Launch, cancellationToken).ConfigureAwait(false);
        if (row.IsError)
        {
            throw new ApplicationFailureException($"{row.Error.Code}: {row.Error.Message}");   // retried: the view could not be read this time
        }

        if (row.Value is not { State: LaunchState.Started })
        {
            // The projector may not have written 'launch.started' into the view yet (#2482): the ledger is the source of truth, so ask it.
            return await EndedInLedger(request, cancellationToken).ConfigureAwait(false) ? Answer(request) : null;
        }

        var state = await sessions.Watch(request.Launch, cancellationToken).ConfigureAwait(false);
        if (state.IsError)
        {
            throw new ApplicationFailureException($"{state.Error.Code}: {state.Error.Message}");
        }

        return state.Value.IsLive ? null : Answer(request);
    }

    private AgentLaunched Answer(LaunchRequest request) =>
        new(request.Worktree, StartHead: null, Failure: null, Agent: request.Agent.Value, RunDirectory: StepEnvironment.RunDirectoryOf(request, home.Path));

    /// <summary>True when the ledger holds the launch's 'launch.started' (not a 'launch.failed' under the same key) and its 'session.ended'. Two key lookups, no scan.</summary>
    private async Task<bool> EndedInLedger(LaunchRequest request, CancellationToken cancellationToken)
    {
        var started = await ledger.ByKey("launch.outcome:" + request.Launch.Value, cancellationToken).ConfigureAwait(false);
        if (started.IsError)
        {
            throw new ApplicationFailureException($"{started.Error.Code}: {started.Error.Message}");
        }

        if (started.Value is not { } first || first.Envelope.Type != EventTypes.LaunchStarted)
        {
            return false;
        }

        var ended = await ledger.ByKey($"{EventTypes.SessionEnded}:{request.Launch.Value}", cancellationToken).ConfigureAwait(false);
        return ended.IsError
            ? throw new ApplicationFailureException($"{ended.Error.Code}: {ended.Error.Message}")
            : ended.Value is not null;
    }
}
