using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ConnectAccount;

/// <summary>A stub until the accounts commit: refuses a caller without the human token (SND-AUTH-002), then connects nothing.</summary>
/// <param name="callers">The caller.</param>
internal sealed class ConnectAccountService(ICallerContextAccessor callers) : IService<ConnectAccountRequest, ConnectAccountResponse>
{
    public ValueTask<Result<ConnectAccountResponse>> Handle(ConnectAccountRequest request, CancellationToken cancellationToken) =>
        OwnerCaller.Refusal(callers, "Connecting an account") is { } refused
            ? ValueTask.FromResult<Result<ConnectAccountResponse>>(refused)
            : throw new NotSupportedException("ConnectAccount is not built yet (first-run spec decisions 13 to 17)");
}
