using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.OpenItem;

/// <summary>
/// Opens an item's agent session (UI spec E-1, API spec §3.2; desk): the checks (<see cref="OpenChecks"/>: the item, a launch in flight, a session to
/// attach to or resume), the agent and its budget (<see cref="OpenTarget"/>), then one 'item.open-requested' keyed by the launch it names. It launches
/// nothing: the launcher service does, within 30 s.
/// </summary>
/// <param name="checks">The item and launch checks.</param>
/// <param name="target">The agent and its budget.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="ledger">Appends the request.</param>
/// <param name="events">Builds the entry.</param>
/// <param name="ids">The id source for the launch.</param>
/// <param name="callers">The caller: the command id.</param>
internal sealed class OpenItemService(OpenChecks checks, OpenTarget target, HeldLease held, ILedgerStore ledger, EventFactory events, IIdGenerator ids, ICallerContextAccessor callers)
    : IService<OpenItemRequest, LaunchRequested>
{
    public async ValueTask<Result<LaunchRequested>> Handle(OpenItemRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("OpenItem needs a caller");
        }

        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var admitted = await checks.Admit(request, caller, cancellationToken).ConfigureAwait(false);
        if (admitted.IsError)
        {
            return admitted.Error;
        }

        var item = admitted.Value.Item;
        var decided = await target.Decide(request, item.Repo, admitted.Value.Session, cancellationToken).ConfigureAwait(false);
        if (decided.IsError)
        {
            return decided.Error;
        }

        var launch = LaunchId.For(ids.NewId());
        var requested = new ItemOpenRequested(launch, item.Id, request.Mode, decided.Value.Target, decided.Value.ChosenBy);
        var entry = events.Entry(EventTypes.ItemOpenRequested, requested, $"{EventTypes.ItemOpenRequested}:{launch.Value}", item.Id.Value);
        var appended = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError
            ? appended.Error
            : new LaunchRequested(new CommandAccepted(caller.CommandId, appended.Value.Sequence), launch, decided.Value.Target, decided.Value.ChosenBy, decided.Value.Warnings);
    }
}
