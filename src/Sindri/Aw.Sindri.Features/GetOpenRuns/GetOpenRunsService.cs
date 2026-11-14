using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.GetOpenRuns;

/// <summary>A stub until the restart commit: reads nothing.</summary>
internal sealed class GetOpenRunsService : IService<GetOpenRunsRequest, GetOpenRunsResponse>
{
    public ValueTask<Result<GetOpenRunsResponse>> Handle(GetOpenRunsRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("GetOpenRuns is not built yet (first-run spec decision 11)");
}
