using System.Text.Json;
using Aw.Connectors.Core;
using Aw.Connectors.Core.Slack;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Trust;
using Aw.Kernel;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Host.IndexSources;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Connections;

namespace Aw.Sindri.Tests.IndexSources;

// #2508: a Slack connected on the Connections page (OAuth) must feed the index with no profile token pointer, and win over a pointer, the order GitHub has had since #2499.
public sealed class SlackRecordFeedsTokenOrderTests
{
    private const string WithPointer = """{"sources":{"slack":{"channels":[{"id":"C0001GEN","name":"general"}],"token":"keychain:aw/slack","workspace":"example"}}}""";
    private const string WithoutPointer = """{"sources":{"slack":{"channels":[{"id":"C0001GEN","name":"general"}],"workspace":"example"}}}""";

    private readonly FakeSecretWriter _pointers = new();
    private readonly InMemoryConnectionStore _connections = new();
    private readonly FakeConnectionSecrets _connectionSecrets = new();

    public SlackRecordFeedsTokenOrderTests()
    {
        _ = _pointers.Store("aw", "slack", "slack-profile-token").Value;
    }

    private static Scrubbers Scrub() => new(new PassFilter(), new PassFilter(), ScrubRuleset.From("0123456789abcdef"));

    private static ContextSettings Settings(string json) => SettingsReader.Read(JsonDocument.Parse(json).RootElement.Clone()).Value;

    private SlackRecordFeeds Feeds(List<string> made) => new(_pointers, new Factory(), (token, _) =>
    {
        made.Add(token);
        return new NoReader();
    }, _connections, _connectionSecrets);

    private async Task Connect(ConnectionStatus status)
    {
        var connection = ConnectionSamples.Oauth(ConnectionSamples.Name("slack", "work"), status);
        _ = (await _connections.Insert(connection, CancellationToken.None)).Value;
        _ = _connectionSecrets.Store(connection.Name, new TokenSet("slack-connection-token", RefreshToken: null, ExpiresAt: null)).Value;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task For_reads_with_the_connected_slack_connection_even_when_the_profile_token_resolves()
    {
        await Connect(ConnectionStatus.Connected);
        var made = new List<string>();

        var feeds = Feeds(made).For(Settings(WithPointer), [], Scrub());

        Assert.Equal("slack", Assert.Single(feeds).Source.Value);
        Assert.Equal("slack-connection-token", Assert.Single(made));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task For_gives_the_feed_from_the_connected_slack_connection_when_the_profile_has_no_token_pointer()
    {
        await Connect(ConnectionStatus.Connected);
        var made = new List<string>();

        var feeds = Feeds(made).For(Settings(WithoutPointer), [], Scrub());

        Assert.Equal("slack", Assert.Single(feeds).Source.Value);
        Assert.Equal("slack-connection-token", Assert.Single(made));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task For_falls_back_to_the_profile_token_when_the_slack_connection_needs_sign_in()
    {
        await Connect(ConnectionStatus.NeedsSignIn);
        var made = new List<string>();

        var feeds = Feeds(made).For(Settings(WithPointer), [], Scrub());

        _ = Assert.Single(feeds);
        Assert.Equal("slack-profile-token", Assert.Single(made));
    }

    [Fact, Trait("Lane", "unit")]
    public void For_reads_with_the_profile_token_when_there_is_no_slack_connection()
    {
        var made = new List<string>();

        var feeds = Feeds(made).For(Settings(WithPointer), [], Scrub());

        _ = Assert.Single(feeds);
        Assert.Equal("slack-profile-token", Assert.Single(made));
    }

    [Fact, Trait("Lane", "unit")]
    public void For_gives_no_feed_with_no_slack_connection_and_no_profile_token_pointer()
    {
        var made = new List<string>();

        var feeds = Feeds(made).For(Settings(WithoutPointer), [], Scrub());

        Assert.Empty(feeds);
        Assert.Empty(made);
    }

    private sealed class Factory : ISlackConnectorFactory
    {
        public IConnector Create(SlackSettings settings, ISlackReader reader, TimeSpan window) => new NoConnector();
    }

    private sealed class NoConnector : IConnector
    {
        public ConnectorDescription Describe() => new("slack", "Slack");

        public ValueTask<Result<ConnectorPage>> Pull(SourceCursor? after, int limit, DateTimeOffset? before, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<IReadOnlyDictionary<string, string>>> Versions(IReadOnlyList<string> keys, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<Unit>> Probe(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class NoReader : ISlackReader
    {
        public ValueTask<Result<SlackMessagePage>> History(string channel, string? oldest, string? latest, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<SlackMessagePage>> Replies(string channel, string threadTs, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<DateTimeOffset?>> Probe(string channel, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class PassFilter : IIngestFilter, IEgressScrubber
    {
        public ScrubbedField Clean(string raw, bool singleLine) => new(raw, Redacted: false, Blocked: false);

        public ScrubbedField Scrub(string value) => Clean(value, singleLine: false);
    }
}
