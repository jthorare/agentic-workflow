using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Features.Design;

/// <summary>'design check': the pure rules over the files a caller holds (line spec §4.3).</summary>
internal sealed class DesignCheckService : IService<DesignCheckRequest, LineCheckResponse>
{
    public ValueTask<Result<LineCheckResponse>> Handle(DesignCheckRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult<Result<LineCheckResponse>>(DesignPacketChecks.Run(new LineFacts(request.Facts), request.Stage));
    }
}
