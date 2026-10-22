using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.Jobs;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Jobs;

/// <summary>A previous process's ledger: the lease held and one 'job.started' appended, whose sequence is the row's.</summary>
internal sealed class RecoveryRig
{
    public RecoveryRig()
    {
        Progress = new ProjectorProgress(H.Clock);
        Registry = new JobRegistry();
        Recovery = new JobRecovery(Views, H.Store, Progress, H.Held, new JobEventWriter(H.Store, H.Held, H.Events, new PassThroughScrubber(), H.Callers), Registry);
    }

    public SliceHarness H { get; } = new();

    public InMemoryJobViews Views { get; } = new();

    public ProjectorProgress Progress { get; }

    public JobRegistry Registry { get; }

    public JobRecovery Recovery { get; }

    public IEnumerable<LedgerRow> Of(string type) => H.Store.Events.Where(e => string.Equals(e.Envelope.Type, type, StringComparison.Ordinal)).Select(e => new LedgerRow(e.Sequence, e.Envelope.Actor, H.Events.Payload<JobFinished>(e)));

    public async Task<JobRow> Left(JobId id, ServiceName? service = null, JobState state = JobState.Running)
    {
        var started = new JobStarted(CommandId.From(RealTime.NewGuid()), id, JobKind.From("RunService"), JobKey.From("heartbeat"), Item: null, Repo: null, service);
        // why: every test holds the lease before it leaves a job behind
        var appended = await H.Store.Append(H.Events.Entry(EventTypes.JobStarted, started, $"job.started:{id.Value}"), H.Held.Current!, TestContext.Current.CancellationToken);
        var row = A.Job().WithId(id).Build() with { Service = service, State = state, Sequence = appended.Value.Sequence };
        Views.Put(row);
        return row;
    }

    public void Caught() => Progress.Publish(new ViewsStatus(H.Store.Events.Count, H.Store.Events.Count, H.Clock.GetUtcNow(), Rebuilding: false, Stalled: false, StalledAt: null, StalledType: null));

    /// <summary>An end event as the tests read it.</summary>
    public sealed record LedgerRow(long Sequence, string Actor, Result<JobFinished> Payload);
}
