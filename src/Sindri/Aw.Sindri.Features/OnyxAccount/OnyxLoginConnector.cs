using Aw.Kernel;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxAccount;

/// <summary>The port of the Onyx setup job onto this slice's steps 6 to 9 with the owner's login.</summary>
internal sealed class OnyxLoginConnector(OnyxAccountService account) : IOnyxLoginConnector
{
    public async ValueTask<Result<OnyxConnected>> Connect(int port, string version, OnyxLogin login, CancellationToken cancellationToken)
    {
        var result = await account.Handle(new OnyxAccountRequest(port, version), login, cancellationToken).ConfigureAwait(false);
        return result.IsError ? result.Error : new OnyxConnected(result.Value.KeyPointer, result.Value.ProfileChanged);
    }
}
