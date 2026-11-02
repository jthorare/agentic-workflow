using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.AdjudicateSortItems;

internal sealed class AdjudicateSortItemsService : IService<AdjudicateSortItemsRequest, AdjudicateSortItemsResponse>
{
    public ValueTask<Result<AdjudicateSortItemsResponse>> Handle(AdjudicateSortItemsRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("AdjudicateSortItems: make AdjudicateSortItemsTests pass (cutover unit 6.4)");
}
