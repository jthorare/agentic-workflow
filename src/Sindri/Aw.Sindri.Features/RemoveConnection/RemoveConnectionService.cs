using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Features.RemoveConnection;

/// <summary>Removes a connection: deletes its secret first, then its row. An unknown name is SND-CONN-001. A secret that cannot be deleted keeps the row (SND-CONN-006), so the pointer is not lost.</summary>
/// <param name="store">The connections.</param>
/// <param name="secrets">Deletes the secret.</param>
internal sealed class RemoveConnectionService(IConnectionStore store, IConnectionSecrets secrets) : IService<RemoveConnectionRequest, RemoveConnectionResponse>
{
    private readonly IConnectionStore _store = store;
    private readonly IConnectionSecrets _secrets = secrets;

    public ValueTask<Result<RemoveConnectionResponse>> Handle(RemoveConnectionRequest request, CancellationToken cancellationToken) => throw Pending();

    // A red stub: the ports are held so the tests build the class the way the green one is built.
    private NotSupportedException Pending()
    {
        _ = (_store, _secrets);
        return new NotSupportedException();
    }
}
