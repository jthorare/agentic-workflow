using System.Net;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Infrastructure.Connections.GitHub;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Connections;

namespace Aw.Sindri.Tests.Connections;

/// <summary>The repo scan's "known" read (first-run spec gap 7) reads with the token of the connected GitHub row, the gh login's `github:gh` row too (decision 14).</summary>
public sealed class GitHubKnownReposTests : IDisposable
{
    private readonly InMemoryConnectionStore _store = new();
    private readonly FakeConnectionSecrets _secrets = new();
    private readonly Handler _handler = new();
    private readonly HttpClient _http;

    public GitHubKnownReposTests()
    {
        _http = new HttpClient(_handler);
    }

    public void Dispose() => _http.Dispose();

    private GitHubKnownRepos Known() => new(_store, _secrets, _http, new ConnectionCredentials(_store, _secrets, new FakeSecretWriter(), new GhRows.Commands()));

    [Fact, Trait("Lane", "unit")]
    public async Task The_gh_login_connection_reads_the_recent_repos_with_the_gh_token()
    {
        _ = (await _store.Insert(GhRows.Row(), CancellationToken.None)).Value;

        var slugs = (await Known().Slugs(CancellationToken.None)).Value;

        Assert.Equal("acme/widgets", Assert.Single(slugs));
        Assert.Equal("Bearer " + GhRows.Token, _handler.Authorization);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_connected_github_row_is_no_slug_and_no_call()
    {
        var slugs = (await Known().Slugs(CancellationToken.None)).Value;

        Assert.Empty(slugs);
        Assert.Null(_handler.Authorization);
    }

    private sealed class Handler : HttpMessageHandler
    {
        public string? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""[{"full_name":"acme/widgets"}]""") });
        }
    }
}
