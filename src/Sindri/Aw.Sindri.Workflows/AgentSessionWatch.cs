using Aw.Redaction;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// What 'WatchAgentNode' does (PR 7): polls the session every <see cref="AgentRuntimeOptions.PollInterval"/> and heartbeats, so a cancel reaches it and a worker restart attaches again to the same
/// session. It returns when the session has ended, with the report the engine refused it for when there was one.
/// </summary>
/// <param name="sessions">The session's state.</param>
/// <param name="options">The poll interval.</param>
/// <param name="refusals">Why a session was ended for a refused report.</param>
internal sealed class AgentSessionWatch(IAgentSessions sessions, AgentRuntimeOptions options, ReportRefusals refusals)
{
    /// <summary>Waits for the launch's session to end (an exit, or no heartbeat for 120 s).</summary>
    /// <param name="id">The launch.</param>
    /// <param name="heartbeat">Tells Temporal the wait is alive.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>How it ended.</returns>
    public async Task<AgentSessionState> Watch(LaunchId id, Action heartbeat, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(heartbeat);
        using var timer = new PeriodicTimer(options.PollInterval);
        var first = true;
        while (true)
        {
            heartbeat();
            var state = await sessions.Watch(id, cancellationToken).ConfigureAwait(false);
            if (state.IsError)
            {
                throw new ApplicationFailureException($"{state.Error.Code}: {state.Error.Message}");   // retried: the facts could not be read this time
            }

            if (!state.Value.IsLive)
            {
                var found = first ? state.Value with { FoundEnded = true } : state.Value;
                return refusals.Take(id) is { } refusal ? found with { Detail = Redactor.Default.Scrub(refusal).Text, Refused = true } : found;   // a blocked scrub yields its placeholder, never the input
            }

            first = false;
            _ = await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
