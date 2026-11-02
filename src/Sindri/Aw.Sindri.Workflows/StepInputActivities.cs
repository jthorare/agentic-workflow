using System.Diagnostics;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The Step inputs' activity (plan decision 1): 'FillInput' runs one input before the Step starts, keeps its scrubbed text
/// in memory only, and returns the metadata, which is all Temporal records.
/// </summary>
/// <param name="runner">Runs the input and shapes what it returned.</param>
/// <param name="texts">Where the text is kept.</param>
/// <param name="contexts">Resolves the repo of an instance whose workflow holds none (a child).</param>
/// <param name="logger">Where each fill's duration is logged (serve.log); none logs nothing.</param>
internal sealed partial class StepInputActivities(IStepInputRunner runner, IInjectionTexts texts, StepContexts contexts, ILogger<StepInputActivities>? logger = null)
{
    /// <summary>Runs the input and keeps its text.</summary>
    /// <param name="request">Which input, for which run.</param>
    /// <returns>The metadata of what it filled.</returns>
    [Activity("FillInput")]
    public Task<FilledInput> FillInputAsync(FillInputRequest request) =>
        FillHeartbeating(request, static () => ActivityExecutionContext.Current.Heartbeat(), InputFillPolicy.BeatEvery, ActivityExecutionContext.Current.CancellationToken);

    /// <summary>Runs the input and keeps its text.</summary>
    /// <param name="request">Which input, for which run.</param>
    /// <param name="cancel">The activity's cancellation.</param>
    /// <returns>The metadata of what it filled.</returns>
    internal async Task<FilledInput> Fill(FillInputRequest request, CancellationToken cancel)
    {
        ArgumentNullException.ThrowIfNull(request);
        var watch = Stopwatch.StartNew();
        var ok = false;
        try
        {
            var filled = await FillOnce(request, cancel).ConfigureAwait(false);
            ok = true;
            return filled with { DurationMs = watch.ElapsedMilliseconds };
        }
        finally
        {
            if (logger is not null)
            {
                Timed(logger, request.Input.Value, watch.ElapsedMilliseconds, ok ? "ok" : "failed");
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "step input {Input} filled in {Ms} ms ({Outcome})")]
    private static partial void Timed(ILogger logger, string input, long ms, string outcome);

    private async Task<FilledInput> FillOnce(FillInputRequest request, CancellationToken cancel)
    {
        var resolved = await contexts.WithRepo(request.Address, request.Context(), cancel).ConfigureAwait(false);
        if (resolved.IsError)
        {
            throw new ApplicationFailureException(resolved.Error.Message, errorType: resolved.Error.Code, nonRetryable: true);   // the ledger cannot say which repo: the input must not run on another's
        }

        var context = resolved.Value;
        Result<FilledOutput> filled;
        try
        {
            filled = await runner.Fill(context, request.Index, cancel).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException) // boundary: whatever an input throws; only its type goes into history, because its message could carry the text it filled
        {
            throw Failed(request, e);
        }

        if (filled.IsError)
        {
            // an input without a runtime or a plan that is invalid can't change by retrying; a throw from the input itself can
            throw new ApplicationFailureException(filled.Error.Message, errorType: filled.Error.Code, nonRetryable: true);
        }

        texts.Put(new InjectionKey(request.Address, request.State, request.Run, request.Index), filled.Value.Texts);
        return filled.Value.Metadata;
    }

    /// <summary>Runs the input while calling <paramref name="heartbeat"/> every <paramref name="every"/>.</summary>
    /// <param name="request">Which input, for which run.</param>
    /// <param name="heartbeat">What reports that the fill is alive.</param>
    /// <param name="every">The beat interval.</param>
    /// <param name="cancel">The activity's cancellation.</param>
    /// <returns>The metadata of what it filled.</returns>
    internal Task<FilledInput> FillHeartbeating(FillInputRequest request, Action heartbeat, TimeSpan every, CancellationToken cancel)
    {
        ArgumentNullException.ThrowIfNull(heartbeat);
        return Beating(request, heartbeat, every, cancel);
    }

    private async Task<FilledInput> Beating(FillInputRequest request, Action heartbeat, TimeSpan every, CancellationToken cancel)
    {
        using var done = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        var beating = Beat(heartbeat, every, done.Token);
        try
        {
            return await Fill(request, cancel).ConfigureAwait(false);
        }
        finally
        {
            await done.CancelAsync().ConfigureAwait(false);
            await beating.ConfigureAwait(false);
        }
    }

    // The beat is the fill's liveness: it stops with the fill, so a worker that dies or hangs the activity goes silent and the heartbeat timeout fails it.
    private static async Task Beat(Action heartbeat, TimeSpan every, CancellationToken stop)
    {
        using var timer = new PeriodicTimer(every);
        try
        {
            heartbeat();
            while (await timer.WaitForNextTickAsync(stop).ConfigureAwait(false))
            {
                heartbeat();
            }
        }
        catch (OperationCanceledException)
        {
            // the fill ended
        }
    }

    /// <summary>Drops the instance's kept texts: its Step ended (done, failed, parked or cancelled).</summary>
    /// <param name="address">The instance address.</param>
    [Activity("DropInjection")]
    public void DropInjectionAsync(string address) => texts.Drop(address);

    /// <summary>The failure an input's throw becomes: its type only, never its message or stack, which could carry the text it filled.</summary>
    private static ApplicationFailureException Failed(FillInputRequest request, Exception thrown) =>
        new($"input {request.Input.Value} failed", errorType: thrown.GetType().Name);
}
