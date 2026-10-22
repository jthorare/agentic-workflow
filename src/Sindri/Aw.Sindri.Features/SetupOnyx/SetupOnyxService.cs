using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.SetupOnyx;

/// <summary>
/// The work of the 'SetupOnyx' job (setup-once spec §5.6). Onyx's own `up` (spec §4.1 steps 1 to 5, idempotent and resumable) brings the stack up; the job then tells the owner where to sign up, opens the page when it can, and parks on 'onyx-account'
/// for up to 30 minutes, holding no job slot. When ConnectOnyx wakes it, the login comes out of its single-use slot, steps 6 to 9 run with it, and the slot is emptied whatever happens: on use, cancel, failure and the time running out.
/// The login never leaves this method except as the body of Onyx's own login request.
/// </summary>
/// <param name="up">Onyx's `up`, as `sindri onyx up` runs it.</param>
/// <param name="account">Steps 6 to 9, taking the login.</param>
/// <param name="slots">Where the login waits for this job.</param>
/// <param name="jobs">This job's context: its id, its progress lines and its wait.</param>
/// <param name="browser">Opens the sign-up page when a browser can be asked.</param>
/// <param name="progress">Reports the lines of `up` (to the job's lines while a job runs).</param>
internal sealed class SetupOnyxService(IOnyxStackUp up, IOnyxLoginConnector account, IOnyxLoginSlots slots, IJobContextAccessor jobs, IBrowser browser, IOnyxProgress progress)
    : IService<SetupOnyxRequest, SetupOnyxResult>
{
    public async ValueTask<Result<SetupOnyxResult>> Handle(SetupOnyxRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var context = jobs.Current;
        if (context?.Waiting is not { } waiting)
        {
            return SindriErrors.ServiceFailed.With("the Onyx setup runs as a job that can wait for the owner's login");
        }

        try
        {
            var started = await up.Up(cancellationToken).ConfigureAwait(false);
            if (started.IsError)
            {
                return started.Error;
            }

            var onyx = started.Value;
            var page = new Uri(OnyxSetupJob.SignupUrl(onyx.Port));
            progress.Line($"needs you: create your Onyx account at {page}, then enter your login in Sindri");
            _ = browser.Open(page);   // best effort: the address is on the step either way
            var came = await waiting.WaitFor(OnyxSetupJob.Signal, OnyxSetupJob.LoginWait, cancellationToken).ConfigureAwait(false);
            if (!came)
            {
                return SindriErrors.SetupOnyxNotWaiting.With("the 30 minutes for your Onyx login ran out (fix: press Set up Onyx and try again)");
            }

            if (slots.Take(context.Job) is not { } login)
            {
                return SindriErrors.ServiceFailed.With("the Onyx login was not there when the setup woke");
            }

            var connected = await account.Connect(onyx.Port, onyx.Version, login, cancellationToken).ConfigureAwait(false);
            return connected.IsError ? connected.Error : new SetupOnyxResult(onyx.Url, onyx.Version, connected.Value.KeyPointer, connected.Value.ProfileChanged);
        }
        finally
        {
            slots.Clear(context.Job);
        }
    }
}
