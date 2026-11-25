using System.Text.Json;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Sync;
using Aw.Context.Core.Trust;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Host;
using Aw.Sindri.Testing.Connections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.IndexSources;

// #2508: the live registration, not a hand-built feed: a Slack connected on the Connections page feeds the index with no profile token pointer.
public sealed class SlackFeedCompositionTests : IDisposable
{
    private const string WithoutPointer = """{"sources":{"slack":{"channels":[{"id":"C0001GEN","name":"general"}],"workspace":"example"}}}""";

    private readonly string _home = Directory.CreateTempSubdirectory("slack-compose-").FullName;
    private readonly InMemoryConnectionStore _connections = new();
    private readonly FakeConnectionSecrets _connectionSecrets = new();

    public void Dispose() => Directory.Delete(_home, recursive: true);

    private static Scrubbers Scrub() => new(new PassFilter(), new PassFilter(), ScrubRuleset.From("0123456789abcdef"));

    private IReadOnlyList<RecordFeed> SlackFeeds()
    {
        var read = SettingsReader.Read(JsonDocument.Parse(WithoutPointer).RootElement.Clone());
        Assert.True(read.IsSuccess);
        string? Environment(string name) => string.Equals(name, "AW_STATE_DIR", StringComparison.Ordinal) || string.Equals(name, "AW_SINDRI_HOME", StringComparison.Ordinal) ? _home : null;
        var services = Composition.Compose(new ServiceCollection(), Environment, new FakeTimeProvider())
            .AddSingleton<IConnectionStore>(_connections)
            .AddSingleton<IConnectionSecrets>(_connectionSecrets);
        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = false, ValidateScopes = true });
        var repo = new ContextRepo(ContextRepoName.From("split-panes"), _home);
        return [.. provider.GetRequiredService<IRecordFeedCatalog>().For(read.Value, [repo], Scrub()).Where(static f => f.Source.Value == "slack")];
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_registered_slack_feed_exists_for_a_connected_slack_connection_with_no_profile_pointer()
    {
        var connection = ConnectionSamples.Oauth(ConnectionSamples.Name("slack", "work"), ConnectionStatus.Connected);
        _ = (await _connections.Insert(connection, CancellationToken.None)).Value;
        _ = _connectionSecrets.Store(connection.Name, new TokenSet("slack-connection-token", RefreshToken: null, ExpiresAt: null)).Value;

        _ = Assert.Single(SlackFeeds());
    }

    [Fact, Trait("Lane", "integration")]
    public void The_registered_slack_feed_is_absent_with_no_connection_and_no_pointer()
        => Assert.Empty(SlackFeeds());

    private sealed class PassFilter : IIngestFilter, IEgressScrubber
    {
        public ScrubbedField Clean(string raw, bool singleLine) => new(raw, Redacted: false, Blocked: false);

        public ScrubbedField Scrub(string value) => Clean(value, singleLine: false);
    }
}
