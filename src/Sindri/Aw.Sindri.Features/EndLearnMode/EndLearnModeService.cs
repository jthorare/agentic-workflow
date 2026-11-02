using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.EndLearnMode;

/// <summary>
/// Ends network learn mode early (sandbox spec §3.6; desk): appends 'sandbox.learn-ended' for one repo or all. The plan builder reads it through the learn history and
/// closes the window at the next launch. Keyed by the repo and the instant, so a retry a moment later is a second row, which is harmless: the latest end wins.
/// </summary>
/// <param name="ledger">Appends the event.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="events">Builds the entry.</param>
/// <param name="callers">The caller: the command id.</param>
/// <param name="clock">When the owner ended it.</param>
internal sealed class EndLearnModeService(ILedgerStore ledger, HeldLease held, EventFactory events, ICallerContextAccessor callers, TimeProvider clock)
    : IService<EndLearnModeRequest, CommandAccepted>
{
    public async ValueTask<Result<CommandAccepted>> Handle(EndLearnModeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("EndLearnMode needs a caller");
        }

        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var at = clock.GetUtcNow();
        var key = string.Create(CultureInfo.InvariantCulture, $"{EventTypes.SandboxLearnEnded}:{request.Repo?.Value ?? "all"}:{at.UtcTicks}");
        var appended = await ledger.Append(events.Entry(EventTypes.SandboxLearnEnded, new SandboxLearnEnded(at, request.Repo), key), grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : new CommandAccepted(caller.CommandId, appended.Value.Sequence);
    }
}
