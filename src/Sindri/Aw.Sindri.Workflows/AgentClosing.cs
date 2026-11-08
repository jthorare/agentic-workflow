using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// What 'CloseAgentStep' does when a run closes (engine P8 D9, D10, D11): takes a final usage sample, reads the session's usage and the engine-measured diff signal, then stops the session.
/// A stop that fails is logged and does not fail the close: the session ends by itself when its agent exits, and the run's result must not wait on the host.
/// </summary>
/// <param name="sessions">The session's usage.</param>
/// <param name="diff">The diff signal.</param>
/// <param name="launcher">Stops the session.</param>
/// <param name="flushUsage">Takes the final usage sample (the sampler is scoped, this class is not).</param>
/// <param name="refusals">Forgets a refusal the watch never took.</param>
/// <param name="logger">Where a failed flush or stop is logged.</param>
internal sealed partial class AgentClosing(IAgentSessions sessions, IRunDiff diff, ILauncher launcher, UsageFlush flushUsage, ReportRefusals refusals, ILogger<AgentClosing> logger)
{
    /// <summary>Measures (when asked) and stops.</summary>
    /// <param name="run">The run.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The tokens spent and whether the diff was empty.</returns>
    public async Task<AgentClosed> Close(AgentCloseRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var launch = LaunchId.From(run.Launch);
        var closed = run.Measure ? await Measure(run, launch, cancellationToken).ConfigureAwait(false) : new AgentClosed(Tokens: 0, EmptyDiff: false);
        refusals.Discard(launch);   // the Step has failed or reported by now; nothing will ask for it
        var stopped = await launcher.Stop(launch, cancellationToken).ConfigureAwait(false);
        if (stopped.IsError)
        {
            StopFailed(logger, run.Launch, stopped.Error.Code);
        }

        return closed;
    }

    private async Task<AgentClosed> Measure(AgentCloseRun run, LaunchId launch, CancellationToken cancellationToken)
    {
        await Flush(launch, cancellationToken).ConfigureAwait(false);   // the ledger's latest sample is up to a minute old, or missing for a short Step
        var usage = await sessions.Usage(launch, cancellationToken).ConfigureAwait(false);
        if (usage.IsError)
        {
            throw new InvalidOperationException($"{usage.Error.Code}: {usage.Error.Message}");   // the activity retries: the run's cost is not guessed
        }

        var empty = run is { Worktree: { } worktree, StartHead: { } head }
            && await diff.IsEmpty(worktree, head, cancellationToken).ConfigureAwait(false) is { IsError: false, Value: true };   // not measured is not empty
        return new AgentClosed((int)Math.Min(usage.Value, int.MaxValue), empty);
    }

    /// <summary>A final usage sample for the launch. One that fails is logged and leaves the last sample: a close must not fail on a sample.</summary>
    private async Task Flush(LaunchId launch, CancellationToken cancellationToken)
    {
        try
        {
            await flushUsage(launch, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)   // boundary: the sample is a refinement of a number the ledger already holds
        {
            FlushFailed(logger, launch.Value, e.GetType().Name);   // the type only: a message could quote transcript text
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "the final usage sample of launch {Launch} threw {Type}; the last sample stands")]
    private static partial void FlushFailed(ILogger logger, string launch, string type);

    [LoggerMessage(Level = LogLevel.Warning, Message = "stopping launch {Launch} failed ({Code}); the session ends when its agent exits")]
    private static partial void StopFailed(ILogger logger, string launch, string code);
}
