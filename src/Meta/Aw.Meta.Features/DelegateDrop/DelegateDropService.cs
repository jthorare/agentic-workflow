using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Logs;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.DelegateDrop;

/// <summary>Removes the delegation of one issue from the ledger, owed or settled, so stop-check stops owing it; logs the drop. An issue with no delegation drops nothing.</summary>
/// <param name="ledger">The recorded delegations.</param>
/// <param name="log">The running log.</param>
/// <param name="clock">Dates the log line.</param>
internal sealed class DelegateDropService(DelegationLedger ledger, IRunningLog log, TimeProvider clock) : IService<DelegateDropRequest, DelegateDropResponse>
{
    public async ValueTask<Result<DelegateDropResponse>> Handle(DelegateDropRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var removed = await ledger.Remove(request.Issue, cancellationToken).ConfigureAwait(false);
        if (removed.IsError)
        {
            return removed.Error;
        }

        if (removed.Value is not { } dropped)
        {
            return new DelegateDropResponse(request.Issue, Dropped: false, Lane: null);
        }

        var appended = await log.Append(LogLine.Event(LogKind.Note, clock.GetUtcNow(), $"delegation {dropped.Marker} to lane {dropped.Lane} dropped: {dropped.Title}"), cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : new DelegateDropResponse(request.Issue, Dropped: true, dropped.Lane);
    }
}
