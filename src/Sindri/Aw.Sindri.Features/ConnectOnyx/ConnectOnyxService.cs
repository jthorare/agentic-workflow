using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.ConnectOnyx;

/// <summary>
/// Hands the owner's login to the Onyx setup job and wakes it (setup-once spec §5.6; desk). Refused as SND-SETUP-006 unless the job waits on 'onyx-account' (nothing is kept then); otherwise the login moves from the request's hand-over into the
/// job's single-use slot and the job runs on with steps 7 to 10. The answer is the same job, as a start's: its links and the ledger head.
/// </summary>
/// <param name="signals">Asks whether the job waits, and wakes it.</param>
/// <param name="slots">The single-use slots.</param>
/// <param name="hand">Where this request's endpoint left the login.</param>
/// <param name="ledger">The head the answer is read-your-writes against.</param>
/// <param name="callers">The caller: the command id.</param>
internal sealed class ConnectOnyxService(IJobSignals signals, IOnyxLoginSlots slots, IOnyxLoginHand hand, ILedgerReader ledger, ICallerContextAccessor callers) : IService<ConnectOnyxRequest, JobStartedResponse>
{
    private static Error NotWaiting => SindriErrors.SetupOnyxNotWaiting.With(SindriErrors.SetupOnyxNotWaiting.Title);

    public async ValueTask<Result<JobStartedResponse>> Handle(ConnectOnyxRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("ConnectOnyx needs a caller");
        }

        var login = hand.Take();
        if (login is null)
        {
            return SindriErrors.SetupOnyxCredentialsMissing.With(SindriErrors.SetupOnyxCredentialsMissing.Title);
        }

        var job = request.Job;
        if (!signals.IsWaiting(job, OnyxSetupJob.Signal))
        {
            return NotWaiting;
        }

        slots.Put(job, login);
        if (!signals.Signal(job, OnyxSetupJob.Signal))
        {
            slots.Clear(job);   // it stopped waiting between the check and the wake: the login is not kept
            return NotWaiting;
        }

        var head = await ledger.Head(cancellationToken).ConfigureAwait(false);
        var sequence = head.IsSuccess ? head.Value : 0;
        return new JobStartedResponse(new CommandAccepted(caller.CommandId, sequence), job,
            new JobLinks($"/sindri/jobs/{job.Value}", $"/sindri/jobs/{job.Value}/cancel", string.Create(CultureInfo.InvariantCulture, $"/sindri/events?after={sequence}&types=job.*")));
    }
}
