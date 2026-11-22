using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Evolve;

namespace Aw.Sindri.Features.GetProposal;

/// <summary>Reads one proposal; an unknown id is SND-PROP-001.</summary>
/// <param name="store">The proposals.</param>
internal sealed class GetProposalService(IProposalStore store) : IService<GetProposalRequest, GetProposalResponse>
{
    public async ValueTask<Result<GetProposalResponse>> Handle(GetProposalRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var found = await store.Find(request.Id, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        if (found.Value is not { } stored)
        {
            return SindriErrors.ProposalNotFound.With(request.Id.Value);
        }

        var original = stored.Original;
        return new GetProposalResponse(ProposalSummary.From(stored), original.Rationale, original.Change.Description, original.Change.Files, original.Evidence, stored.Sessions, stored.Origin, stored.CreatedAt);
    }
}
