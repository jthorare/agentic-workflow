using System.Net;
using System.Text.Json.Nodes;
using Aw.Connectors.Testing.Server;
using Aw.Connectors.Testing.Slack;
using static Aw.Connectors.Testing.Slack.SlackWorkspaceSeed;

namespace Aw.Connectors.Tests.Slack;

/// <summary>The host's command line (messages spec §8.1): the fixture's "a gap and a 429 storm at start" as --gap and --rate-limit.</summary>
public sealed class FakeSlackHostOptionsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static FakeSlackHostOptions Parsed(params string[] args)
    {
        var (options, error) = FakeSlackHostOptions.Parse(args);
        Assert.Null(error);
        return Assert.IsType<FakeSlackHostOptions>(options);
    }

    private static Task<HttpResponseMessage> History(HttpClient client, string channel)
        => client.GetAsync(new Uri("https://slack.com/api/conversations.history?limit=200&channel=" + channel), Ct);

    [Fact, Trait("Lane", "unit")]
    public void No_argument_seeds_the_workspace_on_a_free_port_with_no_script()
    {
        var options = Parsed();

        Assert.Equal(0, options.Port);
        Assert.True(options.Seed);
        Assert.Empty(options.RateLimits);
        Assert.Empty(options.Gaps);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_storm_and_the_gap_are_read_with_their_counts()
    {
        var options = Parsed("--port", "47123", "--rate-limit", "conversations.history=5", "--rate-limit", "search.messages=2:7", "--gap", General + "=12");

        Assert.Equal(47123, options.Port);
        Assert.Equal([("conversations.history", 5, 30), ("search.messages", 2, 7)], options.RateLimits);
        Assert.Equal([(General, 12)], options.Gaps);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("--port", "x")]
    [InlineData("--port", "70000")]
    [InlineData("--port")]
    [InlineData("--rate-limit", "chat.postMessage=3")]
    [InlineData("--rate-limit", "conversations.history")]
    [InlineData("--rate-limit", "conversations.history=0")]
    [InlineData("--rate-limit", "conversations.history=2:0")]
    [InlineData("--rate-limit", "conversations.history=2:3:4")]
    [InlineData("--gap", "C9999NOP=5")]
    [InlineData("--gap", "C0001GEN=-1")]
    [InlineData("--empty", "--gap", "C0001GEN=3")]
    [InlineData("--sideways")]
    public void A_bad_command_line_is_refused_with_a_sentence(params string[] args)
    {
        var (options, error) = FakeSlackHostOptions.Parse(args);

        Assert.Null(options);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Apply_queues_the_429s_for_that_method_and_adds_the_older_messages()
    {
        var options = Parsed("--rate-limit", "conversations.history=2:7", "--gap", General + "=12");
        using var server = options.Apply(new FakeSlackServer());
        using var client = new HttpClient(server, disposeHandler: false);

        using var first = await History(client, General);
        using var info = await client.GetAsync(new Uri("https://slack.com/api/users.info?user=" + Pat), Ct);
        using var second = await History(client, General);
        using var third = await History(client, General);

        Assert.Equal(HttpStatusCode.TooManyRequests, first.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(7), first.Headers.RetryAfter?.Delta);
        Assert.Equal(HttpStatusCode.OK, info.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.Equal(HttpStatusCode.OK, third.StatusCode);
        var messages = JsonNode.Parse(await third.Content.ReadAsStringAsync(Ct))!["messages"]!.AsArray();
        Assert.Equal(14, messages.Count);   // why: the 2 seeded top-level messages and the 12 older ones
        Assert.Equal(OlderNewestTs, messages[2]!["ts"]!.GetValue<string>());
        Assert.Equal("Older message 1 of 12.", messages[13]!["text"]!.GetValue<string>());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_host_started_with_a_storm_answers_429_over_http_then_serves()
    {
        var options = Parsed("--rate-limit", "auth.test=1:3");
        await using var host = await FakeSlackHost.Start(options.Apply(new FakeSlackServer()), options.Port, Ct);
        using var client = new HttpClient { BaseAddress = host.BaseUrl };

        using var limited = await client.GetAsync(new Uri("api/auth.test", UriKind.Relative), Ct);
        using var served = await client.GetAsync(new Uri("api/auth.test", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(3), limited.Headers.RetryAfter?.Delta);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
    }
}
