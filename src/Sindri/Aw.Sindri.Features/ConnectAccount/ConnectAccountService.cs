using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Features.ConnectAccount;

/// <summary>A stub until the accounts commit: refuses a caller without the human token (SND-AUTH-002), then connects nothing.</summary>
/// <param name="callers">The caller.</param>
/// <param name="gh">The gh login on this box.</param>
/// <param name="profile">Writes `review.credential` and clears `setup.skipAccounts`.</param>
/// <param name="connections">The Connections page's rows.</param>
/// <param name="time">Stamps the connection row.</param>
internal sealed class ConnectAccountService(ICallerContextAccessor callers, IGitHubCliLogin gh, AccountProfileWrites profile, IConnectionStore connections, TimeProvider time)
    : IService<ConnectAccountRequest, ConnectAccountResponse>
{
    public ValueTask<Result<ConnectAccountResponse>> Handle(ConnectAccountRequest request, CancellationToken cancellationToken)
    {
        if (OwnerCaller.Refusal(callers, "Connecting an account") is { } refused)
        {
            return ValueTask.FromResult<Result<ConnectAccountResponse>>(refused);
        }

        GC.KeepAlive((gh, profile, connections, time));
        throw new NotSupportedException("ConnectAccount is not built yet (first-run spec decisions 13 to 17)");
    }
}
