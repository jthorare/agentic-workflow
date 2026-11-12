using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ConnectAccount;

/// <summary>A stub until the accounts commit: no caller check, connects nothing.</summary>
internal sealed class ConnectAccountService : IService<ConnectAccountRequest, ConnectAccountResponse>
{
    public ValueTask<Result<ConnectAccountResponse>> Handle(ConnectAccountRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("ConnectAccount is not built yet (first-run spec decisions 13 to 17)");
}
