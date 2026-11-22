using Aw.Transcripts.Core;
using Aw.Transcripts.Features.ListSessions;

namespace Aw.Transcripts.Testing;

/// <summary>Builds a ListSessionsRequest with deterministic defaults: since the day before <see cref="StandardCorpus.Start"/>, every provider, repo and subagent.</summary>
public sealed class ListSessionsRequestBuilder
{
    private ListSessionsRequest _request = new(StandardCorpus.Start.AddDays(-1));

    /// <summary>Lists from <paramref name="since"/>.</summary>
    /// <param name="since">The time.</param>
    /// <returns>This builder.</returns>
    public ListSessionsRequestBuilder Since(DateTimeOffset since)
    {
        _request = _request with { Since = since };
        return this;
    }

    /// <summary>Lists one provider's sessions.</summary>
    /// <param name="provider">The provider.</param>
    /// <returns>This builder.</returns>
    public ListSessionsRequestBuilder Of(TranscriptProvider provider)
    {
        _request = _request with { Provider = provider };
        return this;
    }

    /// <summary>Lists one repo's sessions.</summary>
    /// <param name="repo">The repo path.</param>
    /// <returns>This builder.</returns>
    public ListSessionsRequestBuilder InRepo(string repo)
    {
        _request = _request with { Repo = repo };
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public ListSessionsRequest Build() => _request;
}
