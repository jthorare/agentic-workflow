using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Launches;

namespace Aw.Sindri.Features.RecordLimitHit;

/// <summary>A limit that was reached without ending the session (sandbox spec §3.3): checks the launch secret, then appends 'sandbox.limit-hit' once per launch and kind; a repeat is a no-op success.</summary>
/// <param name="session">The session events and the secret check.</param>
internal sealed class RecordLimitHitService(LaunchSessionEvents session) : IService<RecordLimitHitRequest, RecordLimitHitResponse>
{
    public async ValueTask<Result<RecordLimitHitResponse>> Handle(RecordLimitHitRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var proven = session.Verify(request.LaunchId, request.Secret);
        if (proven.IsError)
        {
            return proven.Error;
        }

        var recorded = await session.LimitHit(request.LaunchId, request.Kind, request.Peak, cancellationToken).ConfigureAwait(false);
        return recorded.IsError ? recorded.Error : new RecordLimitHitResponse(request.LaunchId);
    }
}
