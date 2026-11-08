using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.AddConnection;

/// <summary>Stub (red): signs an existing connection in again.</summary>
internal sealed class SignInConnectionService : IService<SignInConnectionRequest, AddConnectionResponse>
{
    public ValueTask<Result<AddConnectionResponse>> Handle(SignInConnectionRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
