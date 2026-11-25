using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Evolve;

namespace Aw.Sindri.Features.ListProposals;

/// <summary>Reads the proposal store. A session may read it: the list holds no transcript text.</summary>
/// <param name="store">The proposals.</param>
internal sealed class ListProposalsService(IProposalStore store) : IService<ListProposalsRequest, ListProposalsResponse>
{
    public async ValueTask<Result<ListProposalsResponse>> Handle(ListProposalsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var listed = await store.List(request.State, cancellationToken).ConfigureAwait(false);
        return listed.IsError ? listed.Error : new ListProposalsResponse([.. listed.Value.Select(ProposalSummary.From)]);
    }
}
