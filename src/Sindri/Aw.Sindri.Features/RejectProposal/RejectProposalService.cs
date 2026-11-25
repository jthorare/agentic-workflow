using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Evolve;

namespace Aw.Sindri.Features.RejectProposal;

/// <summary>Rejects a proposal. The owner's call: an agent session is refused (SND-AUTH-002), so no agent decides which of its own proposals live.</summary>
/// <param name="store">The proposals.</param>
/// <param name="callers">The current caller.</param>
internal sealed class RejectProposalService(IProposalStore store, ICallerContextAccessor callers) : IService<RejectProposalRequest, RejectProposalResponse>
{
    public async ValueTask<Result<RejectProposalResponse>> Handle(RejectProposalRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current?.Class is not (CallerClass.Human or CallerClass.Service))
        {
            return SindriErrors.AuthRefused.With("Rejecting a proposal is the owner's to do; a session can't (fix: ask the owner to run 'sindri proposal reject')");
        }

        var rejected = await store.Reject(request.Id, cancellationToken).ConfigureAwait(false);
        return rejected.IsError ? rejected.Error : new RejectProposalResponse(ProposalSummary.From(rejected.Value));
    }
}
