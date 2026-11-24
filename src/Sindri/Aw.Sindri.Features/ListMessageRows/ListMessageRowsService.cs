using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ListMessageRows;

/// <summary>A stub until M1f (messages spec §14): the list routes, with the session filter (W17).</summary>
internal sealed class ListMessageRowsService : IService<ListMessageRowsRequest, ListMessageRowsResponse>
{
    public ValueTask<Result<ListMessageRowsResponse>> Handle(ListMessageRowsRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("ListMessageRows is not built yet: messages spec §14 phase M1f");
}
