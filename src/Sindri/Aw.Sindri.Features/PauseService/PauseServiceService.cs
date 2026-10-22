using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.PauseService;

/// <summary>
/// Pauses a service (API P6 unit D; desk): SND-SVC-404 unless the approved profile names it (owner answer Q4), then one 'service.paused' keyed by service
/// and command. A service that is already paused is paused again and answered the same (Q5): the fold is idempotent and the ledger keeps the human's intent.
/// </summary>
/// <param name="approvals">The approved profile, which names the services.</param>
/// <param name="ledger">Appends the event.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="events">Builds the entry.</param>
/// <param name="callers">The caller: the command id.</param>
/// <param name="builtIns">The build's built-in services, which the profile need not name and no one can pause or resume.</param>
internal sealed class PauseServiceService(IApprovedProfile approvals, ILedgerStore ledger, HeldLease held, EventFactory events, ICallerContextAccessor callers,
    IEnumerable<BuiltInService> builtIns)
    : IService<PauseServiceRequest, CommandAccepted>
{
    public async ValueTask<Result<CommandAccepted>> Handle(PauseServiceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("PauseService needs a caller");
        }

        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        if (!ServiceActions.CanPauseOrResume(approved.Value?.Profile.Services?.ContainsKey(request.Name.Value) == true))
        {
            return SindriErrors.ServiceNotFound.With(BuiltInService.Is(request.Name, namedByProfile: false, builtIns)
                ? $"{request.Name.Value} is a built-in service and can't be paused or resumed."
                : $"The approved profile has no service '{request.Name.Value}' (fix: sindri services)");
        }

        var entry = events.Entry(EventTypes.ServicePaused, new ServicePaused(caller.CommandId, request.Name), $"{EventTypes.ServicePaused}:{request.Name.Value}:{caller.CommandId.Value}");
        var appended = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : new CommandAccepted(caller.CommandId, appended.Value.Sequence);
    }
}
