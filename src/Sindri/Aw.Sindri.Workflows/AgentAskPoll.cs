using Aw.Sindri.Core.Graph;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Watches whether a 'run_agent' node's session waits on a permission prompt and records the change (#3151). One long 'AskingAgentNode' call at a time, never while a person holds the node.
/// A call that fails ends the watching: the node goes on as working.
/// </summary>
/// <param name="events">The node's event series.</param>
/// <param name="readEvery">How often the activity reads the state file.</param>
/// <param name="cap">How long one call may run before it hands back with nothing new.</param>
internal sealed class AgentAskPoll(NodeAgentRecorder events, TimeSpan readEvery, TimeSpan cap)
{
    private Flight? _flight;
    private NodeSessionAsk? _seen;
    private bool _failed;

    /// <summary>True when the call in flight has finished and <see cref="Complete"/> has not taken its answer yet.</summary>
    public bool Done => _flight is { Call.IsCompleted: true };

    /// <summary>Starts a call when the clock runs and none is in flight; stops the one in flight while a person holds the node.</summary>
    /// <param name="wait">The node.</param>
    /// <param name="held">Whether a person holds the node.</param>
    /// <returns>Completes when the call is started or stopped.</returns>
    public async Task Sync(AgentWait wait, bool held)
    {
        if (held)
        {
            await Stop().ConfigureAwait(true);
            return;
        }

        if (_flight is null && !_failed)
        {
#pragma warning disable CA2000
            var cancel = new CancellationTokenSource();   // why: the Flight owns it and Complete or Stop disposes it
#pragma warning restore CA2000
            var watch = new AgentNodeAskWatch(wait.Launch, _seen, readEvery, cap);
            _flight = new Flight(Workflow.ExecuteActivityAsync((AgentNodeActivities a) => a.AskingAsync(watch), Options(cancel.Token)), cancel);
        }
    }

    /// <summary>Takes the finished call's answer and records it; the next <see cref="Sync"/> starts the next call.</summary>
    /// <returns>Completes when the event is written.</returns>
    public async Task Complete()
    {
        if (_flight is not { } flight)
        {
            return;
        }

        _flight = null;
        NodeSessionAsk? ask;
        try
        {
            ask = await flight.Call.ConfigureAwait(true);
        }
        catch (ActivityFailureException e) when (!TemporalException.IsCanceledException(e))
        {
            _failed = true;
            ask = null;
        }
        finally
        {
            flight.Cancel.Dispose();
        }

        _seen = ask;
        await (ask is null ? events.Record(NodeAgentStates.Working) : events.Record(NodeAgentStates.WaitingOnYou, "permission prompt", ask)).ConfigureAwait(true);
    }

    /// <summary>Cancels the call in flight, if any. TryCancel does not wait for the activity.</summary>
    /// <returns>Completes when the call has ended.</returns>
    public async Task Stop()
    {
        if (_flight is not { } flight)
        {
            return;
        }

        _flight = null;
#pragma warning disable MA0042
        flight.Cancel.Cancel();   // why: Cancel, not CancelAsync, which would run the callbacks outside the workflow's scheduler
#pragma warning restore MA0042
        try
        {
            _ = await flight.Call.ConfigureAwait(true);
        }
        catch (Exception e) when (TemporalException.IsCanceledException(e) || e is ActivityFailureException)
        {
            // the cancel above, or a failure nobody wants now
        }
        finally
        {
            flight.Cancel.Dispose();
        }
    }

    private ActivityOptions Options(CancellationToken stop) => new()
    {
        StartToCloseTimeout = cap + TimeSpan.FromMinutes(1),
        HeartbeatTimeout = TimeSpan.FromSeconds(30),
        RetryPolicy = new Temporalio.Common.RetryPolicy { MaximumAttempts = 3 },
        CancellationToken = stop,
        CancellationType = ActivityCancellationType.TryCancel,
    };

    private sealed record Flight(Task<NodeSessionAsk?> Call, CancellationTokenSource Cancel);
}
