using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.AddConnection;

/// <summary>Stub for the red commit: the real service replaces a key connection's key once the new key passes its check.</summary>
#pragma warning disable MA0182 // registered in ConnectionServiceCollectionExtensions
internal sealed class ReplaceConnectionKeyService : IService<ReplaceConnectionKeyRequest, AddConnectionResponse>
#pragma warning restore MA0182
{
    public ValueTask<Result<AddConnectionResponse>> Handle(ReplaceConnectionKeyRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
