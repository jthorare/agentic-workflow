using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Delegations;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.Delegated;

/// <summary>Lists the delegations with their state, derived from the recorded messages: owed, sent, or answered (the lane sent a message naming the issue since).</summary>
/// <param name="ledger">The recorded delegations.</param>
/// <param name="messages">The inbound and outbound messages.</param>
internal sealed class DelegatedService(DelegationLedger ledger, IMessageStore messages) : IService<DelegatedRequest, DelegatedResponse>
{
    public async ValueTask<Result<DelegatedResponse>> Handle(DelegatedRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var all = await ledger.All(cancellationToken).ConfigureAwait(false);
        var inbound = await messages.Messages(cancellationToken).ConfigureAwait(false);
        var sent = await messages.Sent(cancellationToken).ConfigureAwait(false);
        if (all.IsError || inbound.IsError || sent.IsError)
        {
            return all.IsError ? all.Error : inbound.IsError ? inbound.Error : sent.Error;
        }

        return new DelegatedResponse([.. all.Value
            .Select(d => (Delegation: d, State: DelegationStates.Of(d, inbound.Value, sent.Value)))
            .Where(p => request.All || p.State != DelegationState.Answered)
            .Select(static p => DelegationView.Of(p.Delegation, p.State))]);
    }
}
