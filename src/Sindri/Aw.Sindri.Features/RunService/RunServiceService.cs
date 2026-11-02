using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.RunService;

/// <summary>
/// The work of a RunService job (API P6 unit D): runs the named service once as 'service:&lt;name&gt;' (the runner enters that caller), claims the
/// <see cref="ServiceRunGuard"/> so no scheduled run overlaps it, records the run as a manual 'service.ran' (keyed by the job; its slot is when it
/// started) and shares the outcome with the scheduler. A cancelled run records nothing: cancelling is not an outcome of the service (D7).
/// </summary>
/// <param name="scheduled">Every service this build has.</param>
/// <param name="guard">Makes a manual and a scheduled run of one service exclusive.</param>
/// <param name="jobs">The running job: its id keys the record, its progress takes the first line.</param>
/// <param name="record">Appends the run's 'service.ran'.</param>
/// <param name="held">The lease the run is fenced by.</param>
/// <param name="clock">Dates the run's slot.</param>
/// <param name="cancellation">Says whether a cancel of this job is under way, so a run it ends records nothing.</param>
internal sealed class RunServiceService(IEnumerable<IScheduledService> scheduled, ServiceRunGuard guard, IJobContextAccessor jobs,
    IServiceRunRecorder record, HeldLease held, TimeProvider clock, IJobCancellation cancellation) : IService<RunServiceRequest, ScheduledResult>
{
    public async ValueTask<Result<ScheduledResult>> Handle(RunServiceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = request.Name;
        var service = scheduled.FirstOrDefault(s => s.Identity() == name);
        if (service is null)
        {
            return SindriErrors.ServiceNotFound.With($"This build doesn't have service '{name.Value}'");
        }

        if (!guard.TryClaimReserved(name, out var claim) && !guard.TryClaimManual(name, out claim))   // the admission reserved it; a direct call claims for itself
        {
            return RunServiceMessages.ScheduledInFlight(name);
        }

        using (claim)   // taken before anything else can refuse the run, so every way out gives the reservation back
        {
            return await Run(service, name, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<Result<ScheduledResult>> Run(IScheduledService service, ServiceName name, CancellationToken cancellationToken)
    {
        if (jobs.Current is not { } job)
        {
            return SindriErrors.ServiceNotFound.With($"This build doesn't have service '{name.Value}'");
        }

        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        job.Progress.Report($"run {name.Value} (manual)");
        var run = new ScheduledRun(grant, clock.GetUtcNow());
        var outcome = await Outcome(service, run, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();   // a cancelled run writes no 'service.ran'
        if (await cancellation.CancelRecorded(job.Job, cancellationToken).ConfigureAwait(false))
        {
            throw new OperationCanceledException(cancellationToken);   // CancelJob is recording the cancel and has not yet tripped the token; a cancel that could not be recorded is withdrawn and the run is recorded as usual
        }

        var recorded = await record.Record(name, run, outcome.Map(static _ => Unit.Value), job.Job, cancellationToken).ConfigureAwait(false);
        if (recorded.IsError)
        {
            guard.Forget(name);   // what the ledger holds is unknown now; the scheduler reads it back if its next run is idle
            return recorded.Error;
        }

        guard.Record(name, outcome.IsError ? ServiceOutcome.Failed : ServiceOutcome.Ok);
        return outcome;   // a failure keeps the service's own message (the job event cleans it) and its code
    }

    /// <summary>How the run ended: the service's result, or a throw as SND-SCHED-001 (a cancellation is not an outcome and passes through).</summary>
    private static async Task<Result<ScheduledResult>> Outcome(IScheduledService service, ScheduledRun run, CancellationToken cancellationToken)
    {
        try
        {
            return await service.Run(run, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException && !cancellationToken.IsCancellationRequested)   // boundary: whatever the service throws is a failed run, as the scheduler's
        {
            return SindriErrors.ServiceFailed.With($"{e.Message} ({e.GetType().Name} at {ThrownBy(e)})");
        }
    }

    /// <summary>The method that threw, as Type.Method: no file paths, nothing from the exception's data. An async method's compiler-made state machine reads as the method it belongs to.</summary>
    private static string ThrownBy(Exception e)
    {
        if (new System.Diagnostics.StackTrace(e).GetFrame(0) is not { } frame || System.Diagnostics.DiagnosticMethodInfo.Create(frame) is not { } method)
        {
            return "unknown";
        }

        var type = (method.DeclaringTypeName ?? string.Empty).Split('+');
        var machine = type.Length > 1 && type[^1].StartsWith('<') && type[^1].IndexOf('>', StringComparison.Ordinal) is > 1;
        var owner = type[machine ? ^2 : ^1];
        var name = machine ? type[^1][1..type[^1].IndexOf('>', StringComparison.Ordinal)] : method.Name;   // <Run>d__3 -> Run
        return $"{owner[(owner.LastIndexOf('.') + 1)..]}.{name}";
    }
}
