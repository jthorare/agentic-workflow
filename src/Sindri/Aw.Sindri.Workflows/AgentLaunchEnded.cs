using Aw.Sindri.Core.Control;
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
        _ = ledger;   // stub: the fix reads the launch's start from the ledger
        var row = await views.Get(request.Launch, cancellationToken).ConfigureAwait(false);
        if (row.IsError)
        {
            throw new ApplicationFailureException($"{row.Error.Code}: {row.Error.Message}");   // retried: the view could not be read this time
        }

        if (row.Value is not { State: LaunchState.Started })
        {
            return null;
        }

        var state = await sessions.Watch(request.Launch, cancellationToken).ConfigureAwait(false);
        if (state.IsError)
        {
            throw new ApplicationFailureException($"{state.Error.Code}: {state.Error.Message}");
        }

        return state.Value.IsLive
            ? null
            : new AgentLaunched(request.Worktree, StartHead: null, Failure: null, Agent: request.Agent.Value, RunDirectory: StepEnvironment.RunDirectoryOf(request, home.Path));
    }
}
