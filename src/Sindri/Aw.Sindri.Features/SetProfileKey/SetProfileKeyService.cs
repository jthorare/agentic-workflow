using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.SetProfileKey;

/// <summary>Plans the edit, checks the edited text the way 'profile validate' checks the file and only then writes it. The profile then waits for the owner's approval; this approves nothing.</summary>
internal sealed class SetProfileKeyService : IService<SetProfileKeyRequest, SetProfileKeyResponse>
{
    public ValueTask<Result<SetProfileKeyResponse>> Handle(SetProfileKeyRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
