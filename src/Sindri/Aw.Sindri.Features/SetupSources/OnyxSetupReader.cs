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
internal sealed class OnyxSetupReader(OnyxStatusReader status, ILedgerReader ledger, EventFactory events, IJobViews jobs)
{
    internal async ValueTask<Result<OnyxFact>> Read(CancellationToken cancellationToken)
    {
        var report = await status.Read(cancellationToken).ConfigureAwait(false);
        if (report.IsError)
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
        return OnyxFact.From(report.Value) with
        {
            Deferred = choice is { Value.Deferred: true },
            Job = job.Phase,
            JobLine = job.Line,
            JobFailure = job.FailureCode is { } code ? (code, job.FailureSentence ?? string.Empty) : null,
        };
    }
}
