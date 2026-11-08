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

    public async ValueTask<Result<RemoveConnectionResponse>> Handle(RemoveConnectionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = ConnectionName.Parse(request.Name);
        if (name.IsError)
        {
            return name.Error;
        }

        var found = await _store.Get(name.Value, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return ConnectionFailures.Safe(found.Error);
        }

        var secretDeleted = false;
        if (found.Value.SecretPointer is { } pointer)
        {
            var deleted = _secrets.Delete(pointer);
            if (deleted.IsError)
            {
                return ConnectionFailures.Safe(deleted.Error);   // the row stays, so the pointer is not lost
            }

            secretDeleted = true;
        }

        var removed = await _store.Remove(name.Value, cancellationToken).ConfigureAwait(false);
        return removed.IsError ? ConnectionFailures.Safe(removed.Error) : new RemoveConnectionResponse(name.Value.ToString(), secretDeleted);
    }
}
