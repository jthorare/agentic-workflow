using Aw.Redaction;
using Aw.Sindri.Core.Skills;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Temporalio.Activities;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The agent runtime's activities (engine P8 U3; spec §5.3, §6.5). 'LaunchAgentStep' starts the run's session (short, retried, idempotent per launch id); 'WatchAgentStep' is long, polls the
/// session every <see cref="AgentRuntimeOptions.PollInterval"/> and heartbeats, so a cancel reaches it and a worker restart attaches again to the same session; 'CloseAgentStep' reads the
/// usage and the diff signal and stops the session. Every activity id carries the run token.
/// </summary>
/// <param name="launching">Launches.</param>
/// <param name="closing">Closes.</param>
/// <param name="sessions">The session's state.</param>
/// <param name="options">The poll interval.</param>
/// <param name="refusals">Why a session was ended for a refused report, for the Step that fails on it.</param>
/// <param name="recovering">Reads the output folder of a session that ended without a report.</param>
/// <param name="skills">Watches a skill block's session and reads its result file at each turn end.</param>
internal sealed class AgentStepActivities(AgentLaunching launching, AgentClosing closing, IAgentSessions sessions, AgentRuntimeOptions options, ReportRefusals refusals, AgentRecovering recovering, SkillStepWatching skills)
{
    /// <summary>Starts the run's session and records the outcome.</summary>
    /// <param name="run">The run.</param>
    /// <returns>Where it runs, or why it did not start.</returns>
    [Activity("LaunchAgentStep")]
    public Task<AgentLaunched> LaunchAsync(AgentLaunchRun run) => launching.Launch(run, ActivityExecutionContext.Current.CancellationToken);

    /// <summary>Waits for the launch's session to end (an exit, or no heartbeat for 120 s). Idempotent: a retry watches the same session.</summary>
    /// <param name="launch">The launch ('LCH-…').</param>
    /// <returns>How it ended.</returns>
    [Activity("WatchAgentStep")]
    public async Task<AgentSessionState> WatchAsync(string launch)
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
                return refusals.Take(id) is { } refusal ? found with { Detail = Redactor.Default.Scrub(refusal).Text, Refused = true } : found;   // a blocked scrub yields its placeholder, never the input
            }

            first = false;
            _ = await timer.WaitForNextTickAsync(context.CancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Waits for a skill block's session to end or to finish: the plain watch, plus the result file read at each turn end (skill flows spec §3.3). Idempotent: a retry watches the same session and reads the same file.</summary>
    /// <param name="watch">The skill launch.</param>
    /// <returns>How the session ended, or the finished result.</returns>
    [Activity("WatchSkillStep")]
    public async Task<AgentSessionState> WatchSkillAsync(SkillWatch watch)
    {
        var context = ActivityExecutionContext.Current;
        var state = await skills.Watch(watch, () => context.Heartbeat(), context.CancellationToken).ConfigureAwait(false);
        return state.EndedAs is not null && refusals.Take(LaunchId.From(watch.Launch)) is { } refusal ? state with { Detail = Redactor.Default.Scrub(refusal).Text, Refused = true } : state;
    }

    /// <summary>Reads the output folder of a session that ended without a report: the result it proves, or why it parks (issue #1000).</summary>
    /// <param name="run">The run.</param>
    /// <returns>The report to complete with, the reason to park with, or nothing.</returns>
    [Activity("RecoverAgentStep")]
    public Task<AgentRecovered> RecoverAsync(AgentRecoverRun run) => recovering.Recover(run, ActivityExecutionContext.Current.CancellationToken);

    /// <summary>Reads the usage and the diff signal, then stops the session.</summary>
    /// <param name="run">The run.</param>
    /// <returns>The tokens spent and whether the diff was empty.</returns>
    [Activity("CloseAgentStep")]
    public Task<AgentClosed> CloseAsync(AgentCloseRun run) => closing.Close(run, ActivityExecutionContext.Current.CancellationToken);
}
