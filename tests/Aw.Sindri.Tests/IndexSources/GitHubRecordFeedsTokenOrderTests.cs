using System.Text.Json;
using Aw.Connectors.Core;
using Aw.Connectors.Core.GitHub;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Trust;
using Aw.Kernel;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Host.IndexSources;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Connections;

namespace Aw.Sindri.Tests.IndexSources;

// #2499: the owner's profile still names the old token the work org rejects (AW-CONNECTORS-007), so a connected GitHub connection must win over it.
public sealed class GitHubRecordFeedsTokenOrderTests
{
    private const string WithPointer = """{"sources":{"github":{"repos":["acme/api"],"token":"keychain:aw/github"}}}""";

    private readonly FakeSecretWriter _pointers = new();
    private readonly InMemoryConnectionStore _connections = new();
    private readonly FakeConnectionSecrets _connectionSecrets = new();

    public GitHubRecordFeedsTokenOrderTests() => _ = _pointers.Store("aw", "github", "ghp_profile").Value;

    private static Scrubbers Scrub() => new(new PassFilter(), new PassFilter(), ScrubRuleset.From("0123456789abcdef"));

    private static ContextSettings Settings(string json) => SettingsReader.Read(JsonDocument.Parse(json).RootElement.Clone()).Value;

    private GitHubRecordFeeds Feeds(List<string> made) => new(_pointers, new Factory(), _connections, _connectionSecrets, (_, _, token) =>
    {
        made.Add(token);
        return new NoReader();
    });

    private async Task Connect(ConnectionStatus status)
    {
        var connection = ConnectionSamples.Oauth(ConnectionSamples.Name("github", "work"), status);
        _ = (await _connections.Insert(connection, CancellationToken.None)).Value;
        _ = _connectionSecrets.Store(connection.Name, new TokenSet("gho_connection", RefreshToken: null, ExpiresAt: null)).Value;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task For_reads_with_the_connected_github_connection_even_when_the_profile_token_resolves()
    {
        await Connect(ConnectionStatus.Connected);
        var made = new List<string>();

        var feeds = Feeds(made).For(Settings(WithPointer), [], Scrub());

        Assert.Equal("github", Assert.Single(feeds).Source.Value);
        Assert.Equal("gho_connection", Assert.Single(made));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task For_falls_back_to_the_profile_token_when_the_connection_needs_sign_in()
    {
        await Connect(ConnectionStatus.NeedsSignIn);
        var made = new List<string>();

        var feeds = Feeds(made).For(Settings(WithPointer), [], Scrub());

        Assert.Single(feeds);
        Assert.Equal("ghp_profile", Assert.Single(made));
    }

    [Fact, Trait("Lane", "unit")]
    public void For_reads_with_the_profile_token_when_there_is_no_github_connection()
    {
        var made = new List<string>();

        var feeds = Feeds(made).For(Settings(WithPointer), [], Scrub());

        Assert.Single(feeds);
        Assert.Equal("ghp_profile", Assert.Single(made));
    }

    private sealed class Factory : IGitHubConnectorFactory
    {
        public IConnector Create(GitHubSettings settings, IGitHubReader reader, TimeSpan window) => new NoConnector();
    }

    private sealed class NoConnector : IConnector
    {
        public ConnectorDescription Describe() => new("github", "GitHub");

        public ValueTask<Result<ConnectorPage>> Pull(SourceCursor? after, int limit, DateTimeOffset? before, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<IReadOnlyDictionary<string, string>>> Versions(IReadOnlyList<string> keys, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<Unit>> Probe(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class NoReader : IGitHubReader
    {
        public ValueTask<Result<GitHubPullPage>> Issues(string repo, GitHubCursor after, int limit, DateTimeOffset? before, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<GitHubCommentPage>> Comments(string repo, DateTimeOffset since, DateTimeOffset until, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<GitHubCommentPage>> IssueComments(string repo, int number, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<GitHubPullDetailRead>> PullRequest(string repo, int number, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<GitHubReviewRead>> PullReviews(string repo, int number, string head, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class PassFilter : IIngestFilter, IEgressScrubber
    {
        public ScrubbedField Clean(string raw, bool singleLine) => new(raw, Redacted: false, Blocked: false);

        public ScrubbedField Scrub(string value) => Clean(value, singleLine: false);
    }
}
