using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Features.Plan;

/// <summary>'plan check': the pure rules over the files a caller holds (line spec §6.3).</summary>
internal sealed class PlanCheckService : IService<PlanCheckRequest, LineCheckResponse>
{
    public ValueTask<Result<LineCheckResponse>> Handle(PlanCheckRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult<Result<LineCheckResponse>>(PlanChecks.Run(new LineFacts(request.Facts), request.Stage));
    }
}
