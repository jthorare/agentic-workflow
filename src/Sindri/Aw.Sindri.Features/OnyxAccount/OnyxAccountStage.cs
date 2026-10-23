using Aw.Kernel;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxAccount;

/// <summary>The port of `onyx attach` onto this slice's steps 6 to 9, asking at the terminal as `onyx up` does.</summary>
internal sealed class OnyxAccountStage(OnyxAccountService account) : IOnyxAccountStage
{
    public async ValueTask<Result<OnyxAccountStageResult>> Run(OnyxOrigin origin, string version, CancellationToken cancellationToken)
    {
        var result = await account.Handle(new OnyxAccountRequest(origin.Port, version, origin.IsLoopback ? null : origin.Url), cancellationToken).ConfigureAwait(false);
        return result.IsError ? result.Error : new OnyxAccountStageResult(result.Value.Url, result.Value.KeyPointer, result.Value.ProfileChanged, result.Value.Steps);
    }
}
