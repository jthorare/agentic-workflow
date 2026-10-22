using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxUp;

/// <summary>The port of the Onyx setup job onto this slice's `up`.</summary>
internal sealed class OnyxStackUp(IService<OnyxUpRequest, OnyxUpResponse> up) : IOnyxStackUp
{
    public async ValueTask<Result<OnyxStackInfo>> Up(CancellationToken cancellationToken)
    {
        var result = await up.Handle(new OnyxUpRequest(), cancellationToken).ConfigureAwait(false);
        return result.IsError ? result.Error : new OnyxStackInfo(result.Value.Port, result.Value.Url, result.Value.Version);
    }
}
