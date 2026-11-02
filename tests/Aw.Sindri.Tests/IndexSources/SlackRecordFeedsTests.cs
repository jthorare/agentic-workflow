using System.Text.Json;
using Aw.Connectors.Core;
using Aw.Connectors.Core.Slack;
using Aw.Connectors.Features;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Trust;
using Aw.Kernel;
using Aw.Sindri.Host.IndexSources;
using Aw.Sindri.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.IndexSources;

public sealed class SlackRecordFeedsTests
{
    private const string Pointer = "keychain:sindri-index/slack";
    private const string Listed = """{"sources":{"slack":{"channels":[{"id":"C0001GEN","name":"general"}],"token":"keychain:sindri-index/slack","workspace":"example"}}}""";

    private readonly FakeSecretWriter _secrets = new();

    private static Scrubbers Scrub() => new(new PassFilter(), new PassFilter(), ScrubRuleset.From("0123456789abcdef"));

    private static ContextSettings Settings(string json) => SettingsReader.Read(JsonDocument.Parse(json).RootElement.Clone()).Value;

    private SlackRecordFeeds Feeds(List<(string Token, string[] Channels)>? made = null)
        => new(_secrets, new Factory(), (token, channels) =>
        {
            made?.Add((token, [.. channels]));
            return new NoReader();
        });

    [Fact, Trait("Lane", "unit")]
    public void A_profile_with_channels_a_workspace_and_a_token_that_resolves_gives_the_slack_feed_over_a_reader_for_those_channels_only()
    {
        _secrets.Stored[Pointer] = "bot-token-value";
        var made = new List<(string Token, string[] Channels)>();

        var feeds = Feeds(made).For(Settings(Listed), [], Scrub());

        Assert.Equal("slack", Assert.Single(feeds).Source.Value);
        var (builtToken, builtChannels) = Assert.Single(made);
        Assert.Equal("bot-token-value", builtToken);
        Assert.Equal(["C0001GEN"], builtChannels, StringComparer.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"sources":{}}""")]
    [InlineData("""{"sources":{"slack":{"channels":[{"id":"C0001GEN","name":"general"}],"workspace":"example"}}}""")]
    [InlineData("""{"sources":{"slack":{"channels":[{"id":"C0001GEN","name":"general"}],"token":"keychain:sindri-index/slack"}}}""")]
    public void A_profile_without_slack_a_token_or_a_workspace_gives_no_feed_and_builds_no_reader(string profile)
    {
        _secrets.Stored[Pointer] = "bot-token-value";
        var made = new List<(string Token, string[] Channels)>();

        var feeds = Feeds(made).For(Settings(profile), [], Scrub());

        Assert.Empty(feeds);
        Assert.Empty(made);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_pointer_that_does_not_resolve_gives_no_feed()
        => Assert.Empty(Feeds().For(Settings(Listed), [], Scrub()));

    [Fact, Trait("Lane", "unit")]
    public void The_host_builds_without_a_slack_reader_registered()
    {
        using var provider = new ServiceCollection().AddSingleton(TimeProvider.System).AddSlackConnectorFactory().BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        Assert.NotNull(provider.GetRequiredService<ISlackConnectorFactory>());
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
