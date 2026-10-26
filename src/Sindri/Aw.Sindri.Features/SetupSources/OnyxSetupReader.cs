using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.SetupSources;

/// <summary>
/// What the Onyx step reads (setup-once spec §6): where Onyx stands (its status, shared with `sindri onyx status`), whether the owner left it for later (the latest 'onyx.deferred' in the ledger), and where the setup job stands (the newest
/// 'SetupOnyx' job in the jobs view). Every member is a read.
/// </summary>
/// <param name="status">The Onyx status.</param>
/// <param name="ledger">Where the owner's choice is read back.</param>
/// <param name="events">Reads the event's payload.</param>
/// <param name="jobs">The jobs view, for the setup job.</param>
/// <param name="clock">Times the status read's budget.</param>
internal sealed class OnyxSetupReader(OnyxStatusReader status, ILedgerReader ledger, EventFactory events, IJobViews jobs, TimeProvider clock)
{
    /// <summary>How long the status read may take (#1121): a Docker that hangs is "unknown" past it, so the setup list answers inside its callers' timeouts.</summary>
    internal static readonly TimeSpan Budget = TimeSpan.FromSeconds(4);

    internal async ValueTask<Result<OnyxFact>> Read(CancellationToken cancellationToken)
    {
        var report = await BoundedStatus(cancellationToken).ConfigureAwait(false);
        if (report is { IsError: true })
        {
            return report.Error;
        }

        var deferred = await ledger.Latest(EventTypes.OnyxDeferred, cancellationToken).ConfigureAwait(false);
        if (deferred.IsError)
        {
            return deferred.Error;
        }

        var choice = deferred.Value is null ? null : events.Payload<OnyxDeferred>(deferred.Value);
        if (choice is { IsError: true })
        {
            return choice.Error;
        }

        var setups = await jobs.List(state: null, OnyxSetupJob.Kind, cancellationToken).ConfigureAwait(false);
        if (setups.IsError)
        {
            return setups.Error;
        }

        var job = OnyxSetupJobFact.From(setups.Value.Count > 0 ? setups.Value[0] : null);
        return (report is null ? new OnyxFact(OnyxState.Unknown, Unreachable: false, BlockedByCode: null) : OnyxFact.From(report.Value)) with
        {
            Deferred = choice is { Value.Deferred: true },
            Job = job.Phase,
            JobLine = job.Line,
            JobFailure = job.FailureCode is { } code ? (code, job.FailureSentence ?? string.Empty) : null,
        };
    }

    // The status read asks the docker CLI synchronously, so it runs on the thread pool and is waited for with the budget; past it the read is cancelled (the runner kills docker)
    // and the answer is null: whether Onyx runs can't be told, which the step shows as a failed read that names Docker.
    private async ValueTask<Result<OnyxStatusReport>?> BoundedStatus(CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);   // every exit with the read still running has its token cancelled first (the timeout cancels it below, the caller's cancel reaches it through the link), so disposing here is safe
        var read = Task.Run(async () => await status.Read(limit.Token).ConfigureAwait(false), CancellationToken.None);
        try
        {
            return await read.WaitAsync(Budget, clock, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            await limit.CancelAsync().ConfigureAwait(false);
            _ = read.ContinueWith(static finished => _ = finished.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);   // why: the abandoned read ends cancelled or faulted; nobody awaits it
            return null;
        }
    }
}
