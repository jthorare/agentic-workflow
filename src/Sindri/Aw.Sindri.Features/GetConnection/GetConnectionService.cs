using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Features.GetConnection;

/// <summary>Reads one connection. A bad name is SND-CONN-008, an unknown one SND-CONN-001.</summary>
/// <param name="store">The connections.</param>
internal sealed class GetConnectionService(IConnectionStore store) : IService<GetConnectionRequest, GetConnectionResponse>
{
    private readonly IConnectionStore _store = store;

    public ValueTask<Result<GetConnectionResponse>> Handle(GetConnectionRequest request, CancellationToken cancellationToken) => throw Pending();

    // A red stub: the ports are held so the tests build the class the way the green one is built.
    private NotSupportedException Pending()
    {
        _ = _store;
        return new NotSupportedException();
    }
}
