using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Transcripts.Features.ListSessions;

internal sealed class ListSessionsService : IService<ListSessionsRequest, ListSessionsResponse>
{
    public ValueTask<Result<ListSessionsResponse>> Handle(ListSessionsRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("ListSessions: make ListSessionsTests pass");
}
