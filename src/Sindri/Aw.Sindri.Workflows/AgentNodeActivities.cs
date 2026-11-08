using System.Diagnostics.CodeAnalysis;
using Aw.Redaction;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Temporalio.Activities;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The agent runtime's activities for a graph node run: the node-run twins of 'LaunchAgentStep' and 'WatchAgentStep', which stay for the recipe engine until it is deleted.</summary>
/// <param name="sessions">The session's state.</param>
/// <param name="options">The poll interval.</param>
/// <param name="refusals">Why a session was ended for a refused report, for the node that fails on it.</param>
internal sealed class AgentNodeActivities(IAgentSessions sessions, AgentRuntimeOptions options, ReportRefusals refusals)
{
    /// <summary>Starts the node run's session; idempotent per launch id.</summary>
    /// <param name="run">The node run.</param>
    /// <returns>Where it runs, or why it did not start.</returns>
    [Activity("LaunchAgentNode")]
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "red stub: the launch reads the instance's launcher once it is wired")]
    public Task<AgentLaunched> LaunchNodeAsync(AgentNodeRun run) => throw new NotSupportedException();

    /// <summary>Waits for the launch's session to end (an exit, or no heartbeat for 120 s). Idempotent: a retry watches the same session.</summary>
    /// <param name="launch">The launch ('LCH-…').</param>
    /// <returns>How it ended.</returns>
    [Activity("WatchAgentNode")]
    public async Task<AgentSessionState> WatchNodeAsync(string launch)
    {
        var context = ActivityExecutionContext.Current;
        var id = LaunchId.From(launch);
        using var timer = new PeriodicTimer(options.PollInterval);
        var first = true;
        while (true)
        {
            context.Heartbeat();
            var state = await sessions.Watch(id, context.CancellationToken).ConfigureAwait(false);
            if (state.IsError)
            {
                throw new ApplicationFailureException($"{state.Error.Code}: {state.Error.Message}");   // retried: the facts could not be read this time
            }

            if (!state.Value.IsLive)
            {
                var found = first ? state.Value with { FoundEnded = true } : state.Value;
                return refusals.Take(id) is { } refusal ? found with { Detail = Redactor.Default.Scrub(refusal).Text, Refused = true } : found;
            }

            first = false;
            _ = await timer.WaitForNextTickAsync(context.CancellationToken).ConfigureAwait(false);
        }
    }
}
