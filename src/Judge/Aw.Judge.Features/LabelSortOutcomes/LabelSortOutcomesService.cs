using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.LabelSortOutcomes;

internal sealed class LabelSortOutcomesService : IService<LabelSortOutcomesRequest, LabelSortOutcomesResponse>
{
    public ValueTask<Result<LabelSortOutcomesResponse>> Handle(LabelSortOutcomesRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("LabelSortOutcomes: make LabelSortOutcomesTests pass (cutover unit 6.4)");
}
