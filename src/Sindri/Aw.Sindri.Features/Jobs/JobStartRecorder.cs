using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.Jobs;

/// <summary>Appends 'job.started' for the start service (API P6 unit C), fenced by the held lease, as the starting caller.</summary>
/// <param name="held">The lease the start is fenced by.</param>
/// <param name="ledger">Appends the event.</param>
/// <param name="events">Builds the entry.</param>
internal sealed class JobStartRecorder(HeldLease held, ILedgerStore ledger, EventFactory events)
{
    /// <summary>Records the start of the job.</summary>
    /// <param name="ticket">The new job: its id, kind and key.</param>
    /// <param name="caller">The starting caller (the event carries its actor and correlation).</param>
    /// <param name="service">The background service the job runs (RunService), or null.</param>
    /// <param name="repo">The job's repo, or null.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The grant it wrote under, the sequence of 'job.started' and its envelope id; or why it could not be written.</returns>
    public async ValueTask<Result<RecordedStart>> Record(JobTicket ticket, CallerContext caller, ServiceName? service, RepoName? repo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);
        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var started = new JobStarted(caller.CommandId, ticket.Job, ticket.Kind, ticket.Key, Item: null, repo, service);
        var entry = events.Entry(EventTypes.JobStarted, started, $"{EventTypes.JobStarted}:{ticket.Job.Value}");
        var appended = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : new RecordedStart(grant, appended.Value.Sequence, EventId.From(entry.Envelope.Id));
    }
}
