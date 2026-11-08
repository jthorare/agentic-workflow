using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Features.GetConnection;

/// <summary>Reads one connection. A bad name is SND-CONN-008, an unknown one SND-CONN-001.</summary>
/// <param name="store">The connections.</param>
internal sealed class GetConnectionService(IConnectionStore store) : IService<GetConnectionRequest, GetConnectionResponse>
{
    private readonly IConnectionStore _store = store;

    public async ValueTask<Result<GetConnectionResponse>> Handle(GetConnectionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = ConnectionName.Parse(request.Name);
        if (name.IsError)
        {
            return name.Error;
        }

        var found = await _store.Get(name.Value, cancellationToken).ConfigureAwait(false);
        return found.IsError ? ConnectionFailures.Safe(found.Error) : new GetConnectionResponse(ConnectionView.From(found.Value));
    }
}
