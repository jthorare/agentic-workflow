using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Features.ListConnections;

/// <summary>Lists every connection as a view (no secret, no pointer).</summary>
/// <param name="store">The connections.</param>
internal sealed class ListConnectionsService(IConnectionStore store) : IService<ListConnectionsRequest, ListConnectionsResponse>
{
    private readonly IConnectionStore _store = store;

    public async ValueTask<Result<ListConnectionsResponse>> Handle(ListConnectionsRequest request, CancellationToken cancellationToken)
    {
        var listed = await _store.List(cancellationToken).ConfigureAwait(false);
        return listed.IsError
            ? ConnectionFailures.Safe(listed.Error)
            : new ListConnectionsResponse([.. listed.Value.Select(ConnectionView.From)]);
    }
}
