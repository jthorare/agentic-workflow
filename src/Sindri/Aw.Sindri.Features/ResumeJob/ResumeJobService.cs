using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.ResumeJob;

/// <summary>Wakes a job parked on 'model-limit' (model routing spec §5.3; desk).</summary>
internal sealed class ResumeJobService : IService<ResumeJobRequest, JobStartedResponse>
{
    /// <summary>Creates the service.</summary>
    /// <param name="signals">Asks whether the job waits, and wakes it.</param>
    /// <param name="ledger">The head the answer is read-your-writes against.</param>
    /// <param name="callers">The caller: the command id.</param>
    public ResumeJobService(IJobSignals signals, ILedgerReader ledger, ICallerContextAccessor callers)
    {
        ArgumentNullException.ThrowIfNull(signals);
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(callers);
    }

    public ValueTask<Result<JobStartedResponse>> Handle(ResumeJobRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
