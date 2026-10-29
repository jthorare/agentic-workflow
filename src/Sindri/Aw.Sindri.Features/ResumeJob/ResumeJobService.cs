using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.ResumeJob;

/// <summary>
/// Wakes a job parked on 'model-limit' (model routing spec §5.3; desk): SND-JOB-003 unless the job waits on exactly that signal now, otherwise the job is signalled and the call that stalled walks its route again from the first harness. The
/// answer is the same job, as a start's: its links and the ledger head ('job.resumed' is recorded by the wait itself).
/// </summary>
/// <param name="signals">Asks whether the job waits, and wakes it.</param>
/// <param name="ledger">The head the answer is read-your-writes against.</param>
/// <param name="callers">The caller: the command id.</param>
internal sealed class ResumeJobService(IJobSignals signals, ILedgerReader ledger, ICallerContextAccessor callers) : IService<ResumeJobRequest, JobStartedResponse>
{
    private const string Signal = "model-limit";

    private static Error NotWaiting => SindriErrors.JobNotWaiting.With("The job is not waiting for a model harness (it never was, it already resumed, or it ended).");

    public async ValueTask<Result<JobStartedResponse>> Handle(ResumeJobRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("ResumeJob needs a caller");
        }

        var job = request.Job;
        if (!signals.IsWaiting(job, Signal) || !signals.Signal(job, Signal))
        {
            return NotWaiting;
        }

        var head = await ledger.Head(cancellationToken).ConfigureAwait(false);
        var sequence = head.IsSuccess ? head.Value : 0;
        return new JobStartedResponse(new CommandAccepted(caller.CommandId, sequence), job,
            new JobLinks($"/sindri/jobs/{job.Value}", $"/sindri/jobs/{job.Value}/cancel", string.Create(CultureInfo.InvariantCulture, $"/sindri/events?after={sequence}&types=job.*")));
    }
}
