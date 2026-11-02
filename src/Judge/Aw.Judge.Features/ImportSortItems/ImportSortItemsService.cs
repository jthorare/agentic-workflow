using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.ImportSortItems;

internal sealed class ImportSortItemsService : IService<ImportSortItemsRequest, ImportSortItemsResponse>
{
    public ValueTask<Result<ImportSortItemsResponse>> Handle(ImportSortItemsRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("ImportSortItems: make ImportSortItemsTests pass (cutover unit 6.4)");
}
