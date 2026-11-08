using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Features.ListConnections;

/// <summary>Lists every connection as a view (no secret, no pointer).</summary>
/// <param name="store">The connections.</param>
internal sealed class ListConnectionsService(IConnectionStore store) : IService<ListConnectionsRequest, ListConnectionsResponse>
{
    private readonly IConnectionStore _store = store;

    public ValueTask<Result<ListConnectionsResponse>> Handle(ListConnectionsRequest request, CancellationToken cancellationToken) => throw Pending();

    // A red stub: the ports are held so the tests build the class the way the green one is built.
    private NotSupportedException Pending()
    {
        _ = _store;
        return new NotSupportedException();
    }
}
