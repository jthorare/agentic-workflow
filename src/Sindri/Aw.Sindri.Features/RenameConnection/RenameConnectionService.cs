using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.RenameConnection;

/// <summary>Changes a connection's label and keeps its secret (red stub: the behaviour arrives with the next commit).</summary>
internal sealed class RenameConnectionService : IService<RenameConnectionRequest, RenameConnectionResponse>
{
    public ValueTask<Result<RenameConnectionResponse>> Handle(RenameConnectionRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("RenameConnectionService is not implemented yet.");
}
