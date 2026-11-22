using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Transcripts.Core.Ports;
using Aw.Transcripts.Core.Sessions;

namespace Aw.Transcripts.Features.ListSessions;

internal sealed class ListSessionsService(ITranscriptCorpus corpus) : IService<ListSessionsRequest, ListSessionsResponse>
{
    public async ValueTask<Result<ListSessionsResponse>> Handle(ListSessionsRequest request, CancellationToken cancellationToken)
    {
        var query = new SessionQuery(request.Since) { Provider = request.Provider, Repo = request.Repo, IncludeSubagents = request.IncludeSubagents };
        var sessions = await corpus.ListSessions(query, cancellationToken).ConfigureAwait(false);
        return sessions.Map(list => new ListSessionsResponse(list));
    }
}
