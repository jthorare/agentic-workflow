using Aw.Transcripts.Features.ListSessions;

namespace Aw.Transcripts.Testing;

/// <summary>Builds a ListSessionsRequest with deterministic defaults.</summary>
public sealed class ListSessionsRequestBuilder
{
    private readonly ListSessionsRequest _request = new();

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public ListSessionsRequest Build() => _request;
}
