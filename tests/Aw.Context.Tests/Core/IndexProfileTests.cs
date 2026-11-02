using System.Text.Json;
using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Context.Tests.Core;

/// <summary>The owned index's profile keys (spec §5.3 to §5.6, §6, §8, Q7): 'context.sources.github', '.slack' and 'context.index', their defaults and each refusal at its key path.</summary>
public sealed class IndexProfileTests
{
    private static Result<ContextSettings> Read(string json) => SettingsReader.Read(JsonDocument.Parse(json).RootElement.Clone());

    private static void Refused(string json, string keyPath)
    {
        var settings = Read(json);

        Assert.True(settings.IsError);
        Assert.Equal("SND-CONTEXT-001", settings.Error.Code);
        Assert.StartsWith(keyPath + ":", settings.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_section_gives_the_index_defaults()
    {
        var index = SettingsReader.Read(section: null).Value.Index;

        Assert.Equal((183, 60_000, null, false), (index.WindowDays, index.MaxChunks, index.RetainDays, index.AcknowledgeOnyxLeftover));
        Assert.Equal((0.5, 0.75, 8), (index.Floors.KeepDistance, index.Floors.RescueDistance, index.Floors.RescueHits));
        Assert.Null(SettingsReader.Read(section: null).Value.GitHub);
        Assert.Null(SettingsReader.Read(section: null).Value.Slack);
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_index_key_is_read()
    {
        var index = Read("""{"index":{"windowDays":90,"maxChunks":1000,"retainDays":365,"floors":{"keepDistance":0.4,"rescueDistance":0.6,"rescueHits":3},"acknowledgeOnyxLeftover":true}}""").Value.Index;

        Assert.Equal((90, 1000, 365, true), (index.WindowDays, index.MaxChunks, index.RetainDays, index.AcknowledgeOnyxLeftover));
        Assert.Equal((0.4, 0.6, 3), (index.Floors.KeepDistance, index.Floors.RescueDistance, index.Floors.RescueHits));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"index":{"windowDays":0}}""", "context.index.windowDays")]
    [InlineData("""{"index":{"windowDays":-5}}""", "context.index.windowDays")]
    [InlineData("""{"index":{"windowDays":100000}}""", "context.index.windowDays")]
    [InlineData("""{"index":{"maxChunks":0}}""", "context.index.maxChunks")]
    [InlineData("""{"index":{"retainDays":0}}""", "context.index.retainDays")]
    [InlineData("""{"index":{"floors":{"keepDistance":0}}}""", "context.index.floors.keepDistance")]
    [InlineData("""{"index":{"floors":{"keepDistance":2.5}}}""", "context.index.floors.keepDistance")]
    [InlineData("""{"index":{"floors":{"rescueDistance":2.5}}}""", "context.index.floors.rescueDistance")]
    [InlineData("""{"index":{"floors":{"keepDistance":0.8,"rescueDistance":0.6}}}""", "context.index.floors.rescueDistance")]
    [InlineData("""{"index":{"floors":{"rescueHits":-1}}}""", "context.index.floors.rescueHits")]
    [InlineData("""{"index":{"floors":{"rescueHits":500}}}""", "context.index.floors.rescueHits")]
    public void An_index_value_out_of_range_is_SND_CONTEXT_001_at_its_key(string json, string keyPath) => Refused(json, keyPath);

    [Fact, Trait("Lane", "unit")]
    public void An_unknown_index_key_is_refused()
    {
        Assert.True(Read("""{"index":{"windowDay":90}}""").IsError);
        Assert.True(Read("""{"index":[]}""").IsError);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_github_entry_is_read_with_its_repos_host_and_token_pointer()
    {
        var github = Read("""{"sources":{"github":{"repos":["acme/web","acme/api"],"host":"ghe.acme.com","token":"env:GH_TOKEN"}}}""").Value.GitHub!;

        Assert.Equal(["acme/web", "acme/api"], github.Repos);
        Assert.Equal("ghe.acme.com", github.Host);
        Assert.False(github.AllowPrivateHost);
        Assert.Equal("env:GH_TOKEN", github.Token);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_github_entry_without_a_host_reads_api_github_com_and_needs_no_token_yet()
    {
        var github = Read("""{"sources":{"github":{"repos":["acme/web"]}}}""").Value.GitHub!;

        Assert.Null(github.Host);
        Assert.Null(github.Token);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"sources":{"github":{"repos":["web"]}}}""", "context.sources.github.repos[0]")]
    [InlineData("""{"sources":{"github":{"repos":["acme/web/x"]}}}""", "context.sources.github.repos[0]")]
    [InlineData("""{"sources":{"github":{"repos":["acme/web","acme/web"]}}}""", "context.sources.github.repos[1]")]
    [InlineData("""{"sources":{"github":{"repos":[""]}}}""", "context.sources.github.repos[0]")]
    [InlineData("""{"sources":{"github":{"host":"https://ghe.acme.com"}}}""", "context.sources.github.host")]
    [InlineData("""{"sources":{"github":{"host":"ghe.acme.com/api"}}}""", "context.sources.github.host")]
    [InlineData("""{"sources":{"github":{"host":"user@ghe.acme.com"}}}""", "context.sources.github.host")]
    [InlineData("""{"sources":{"github":{"host":"8.8.8.8"}}}""", "context.sources.github.host")]
    [InlineData("""{"sources":{"github":{"host":"[::1]"}}}""", "context.sources.github.host")]
    [InlineData("""{"sources":{"github":{"host":"10.0.0.5"}}}""", "context.sources.github.host")]
    [InlineData("""{"sources":{"github":{"host":"169.254.169.254"}}}""", "context.sources.github.host")]
    [InlineData("""{"sources":{"github":{"host":"localhost"}}}""", "context.sources.github.host")]
    public void A_bad_github_repo_or_host_is_refused_at_its_key(string json, string keyPath) => Refused(json, keyPath);

    [Theory, Trait("Lane", "unit")]
    [InlineData("10.0.0.5")]
    [InlineData("127.0.0.1:8443")]
    [InlineData("localhost")]
    public void The_profile_can_mark_a_private_host_as_intended(string host)
    {
        var github = Read("""{"sources":{"github":{"host":"%HOST%","allowPrivateHost":true}}}""".Replace("%HOST%", host, StringComparison.Ordinal)).Value.GitHub!;

        Assert.Equal((host, true), (github.Host, github.AllowPrivateHost));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_token_that_is_not_a_secret_pointer_is_refused_and_never_echoed()
    {
        var settings = Read("""{"sources":{"github":{"token":"ghp_SUPERSECRET123"}}}""");

        Assert.Equal("SND-CONTEXT-002", settings.Error.Code);
        Assert.DoesNotContain("SUPERSECRET", settings.Error.Message, StringComparison.Ordinal);
        Assert.StartsWith("context.sources.github.token:", settings.Error.Message, StringComparison.Ordinal);
        Assert.Equal("SND-CONTEXT-002", Read("""{"sources":{"slack":{"token":"xoxb-SUPERSECRET"}}}""").Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void ToString_withholds_the_token_pointer()
    {
        var settings = Read("""{"sources":{"github":{"repos":["acme/web"],"token":"env:GH_TOKEN_NAME"},"slack":{"channels":[{"id":"C0123ABC","name":"eng"}],"token":"env:SLACK_TOKEN_NAME"}}}""").Value;

        Assert.DoesNotContain("GH_TOKEN_NAME", settings.GitHub!.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("SLACK_TOKEN_NAME", settings.Slack!.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_slack_entry_is_read_with_its_channels_retention_and_token()
    {
        var slack = Read("""{"sources":{"slack":{"channels":[{"id":"C0123ABC","name":"eng"},{"id":"G9XYZ01","name":"leads"}],"retainDays":90,"token":"keychain:slack/bot"}}}""").Value.Slack!;

        Assert.Equal([new SlackChannel("C0123ABC", "eng"), new SlackChannel("G9XYZ01", "leads")], slack.Channels);
        Assert.Equal(90, slack.RetainDays);
        Assert.Equal("keychain:slack/bot", slack.Token);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"sources":{"slack":{"channels":["C0123ABC"]}}}""", "context.sources.slack.channels[0]")]
    [InlineData("""{"sources":{"slack":{"channels":[{"id":"C0123ABC"}]}}}""", "context.sources.slack.channels[0].name")]
    [InlineData("""{"sources":{"slack":{"channels":[{"name":"eng"}]}}}""", "context.sources.slack.channels[0].id")]
    [InlineData("""{"sources":{"slack":{"channels":[{"id":"eng","name":"eng"}]}}}""", "context.sources.slack.channels[0].id")]
    [InlineData("""{"sources":{"slack":{"channels":[{"id":"D0123ABC","name":"a dm"}]}}}""", "context.sources.slack.channels[0].id")]
    [InlineData("""{"sources":{"slack":{"channels":[{"id":"C0123ABC","name":"a"},{"id":"C0123ABC","name":"b"}]}}}""", "context.sources.slack.channels[1].id")]
    [InlineData("""{"sources":{"slack":{"channels":[{"id":"C0123ABC","name":""}]}}}""", "context.sources.slack.channels[0].name")]
    [InlineData("""{"sources":{"slack":{"retainDays":0}}}""", "context.sources.slack.retainDays")]
    public void A_bad_slack_channel_or_retention_is_refused_at_its_key(string json, string keyPath) => Refused(json, keyPath);

    [Fact, Trait("Lane", "unit")]
    public void The_linear_teams_key_is_unchanged()
    {
        var settings = Read("""{"sources":{"linear":{"teams":["ENG","OPS"]}}}""").Value;

        Assert.Equal(["ENG", "OPS"], settings.LinearTeams);
        Refused("""{"sources":{"linear":{"teams":["eng"]}}}""", "context.sources.linear.teams");
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unknown_source_key_is_still_refused()
    {
        Refused("""{"sources":{"github":{"repo":["a/b"]}}}""", "context.sources.github.repo");
        Refused("""{"sources":{"discord":{}}}""", "context.sources.discord");
    }
}
