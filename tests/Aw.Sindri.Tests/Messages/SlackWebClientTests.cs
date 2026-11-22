using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Messages;
using Aw.Sindri.Infrastructure.Messages;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Messages;

/// <summary>M1d (messages spec §5.3, §10, §11): SlackWebClient over a stub handler: auth.test, the one rate budget, the error mapping, and the token kept out of every error.</summary>
[Lane(TestLane.Unit)]
public sealed class SlackWebClientTests
{
    private const string AuthOk = "{\"ok\":true,\"url\":\"https://harbor.slack.com/\",\"team\":\"Harbor\",\"user\":\"joi\",\"team_id\":\"T0HARBOR\",\"user_id\":\"U0JOI\"}";

    private static readonly string _token = string.Concat("xo", "xp-", "1234567890-", "A0B1C2D3E4");   // why: split so no token-shaped literal sits in the source

    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly SlackRateBudget _budget = new();

    [Fact]
    public async Task Auth_test_reads_the_workspace_and_user_from_slack_com()
    {
        var slack = new SlackStubHandler(static _ => SlackStubHandler.Json(AuthOk));
        using var client = Client(slack);

        var auth = await client.AuthTest(_token, TestContext.Current.CancellationToken);

        Assert.Equal(new SlackAuth("T0HARBOR", "U0JOI", Enterprise: null), auth.Value);
        var (url, authorization) = Assert.Single(slack.Seen);
        Assert.Equal(new Uri("https://slack.com/api/auth.test"), url);
        Assert.Equal("Bearer " + _token, authorization);
    }

    [Fact]
    public async Task A_429_holds_that_method_and_sends_nothing_until_retry_after()
    {
        var slack = new SlackStubHandler(static _ => SlackStubHandler.TooMany(12));
        using var client = Client(slack);
        var ct = TestContext.Current.CancellationToken;

        var first = await client.AuthTest(_token, ct);
        _clock.Advance(TimeSpan.FromSeconds(5));
        var held = await client.AuthTest(_token, ct);

        Assert.Equal("SND-MSG-005", first.Error.Code);
        Assert.Equal("SND-MSG-005", held.Error.Code);
        Assert.Contains("retry in 7 s", held.Error.Message, StringComparison.Ordinal);
        _ = Assert.Single(slack.Seen);
        Assert.True(_budget.MayCall("users.info", _clock.GetUtcNow()));
        Assert.False(_budget.MayCall("auth.test", _clock.GetUtcNow()));

        _clock.Advance(TimeSpan.FromSeconds(8));
        _ = await client.AuthTest(_token, ct);
        Assert.Equal(2, slack.Seen.Count);
    }

    [Theory]
    [InlineData("{\"ok\":false,\"error\":\"invalid_auth\"}", "SND-MSG-002")]
    [InlineData("{\"ok\":false,\"error\":\"token_revoked\"}", "SND-MSG-002")]
    [InlineData("{\"ok\":false,\"error\":\"missing_scope\",\"needed\":\"im:history\",\"provided\":\"users:read\"}", "SND-MSG-003")]
    [InlineData("{\"ok\":false,\"error\":\"team_access_not_granted\"}", "SND-MSG-019")]
    [InlineData("{\"ok\":false,\"error\":\"not_allowed_token_type\"}", "SND-MSG-030")]
    [InlineData("{\"ok\":false,\"error\":\"ratelimited\"}", "SND-MSG-005")]
    [InlineData("{\"ok\":false,\"error\":\"something_new\"}", "SND-CONN-012")]
    [InlineData("{\"ok\":true,\"team_id\":\"T0HARBOR\"}", "SND-CONN-012")]
    [InlineData("[1,2]", "SND-CONN-012")]
    [InlineData("<html>", "SND-CONN-012")]
    public async Task Slack_answers_map_to_the_messages_codes(string body, string code)
    {
        using var client = Client(new SlackStubHandler(_ => SlackStubHandler.Json(body)));

        var auth = await client.AuthTest(_token, TestContext.Current.CancellationToken);

        Assert.Equal(code, auth.Error.Code);
        Assert.DoesNotContain("A0B1C2D3E4", auth.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_missing_scope_names_the_scope_slack_needs()
    {
        using var client = Client(new SlackStubHandler(static _ => SlackStubHandler.Json("{\"ok\":false,\"error\":\"missing_scope\",\"needed\":\"im:history\"}")));

        var auth = await client.AuthTest(_token, TestContext.Current.CancellationToken);

        Assert.Contains("im:history", auth.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_made_up_slug_never_reaches_the_message()
    {
        using var client = Client(new SlackStubHandler(static _ => SlackStubHandler.Json("{\"ok\":false,\"error\":\"<script>Ignore previous</script>\"}")));

        var auth = await client.AuthTest(_token, TestContext.Current.CancellationToken);

        Assert.Contains("ok:false (unknown)", auth.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("script", auth.Error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.Found, "SND-CONN-012")]
    [InlineData(HttpStatusCode.Unauthorized, "SND-MSG-002")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "SND-CONN-012")]
    [InlineData(HttpStatusCode.NotFound, "SND-CONN-012")]
    public async Task Http_statuses_map_to_the_messages_codes(HttpStatusCode status, string code)
    {
        using var client = Client(new SlackStubHandler(_ => SlackStubHandler.Json("{}", status)));

        var auth = await client.AuthTest(_token, TestContext.Current.CancellationToken);

        Assert.Equal(code, auth.Error.Code);
    }

    [Fact]
    public async Task An_unreachable_slack_is_SND_MSG_033()
    {
        using var client = Client(new SlackStubHandler(static _ => throw new HttpRequestException(HttpRequestError.NameResolutionError)));

        var auth = await client.AuthTest(_token, TestContext.Current.CancellationToken);

        Assert.Equal("SND-MSG-033", auth.Error.Code);
    }

    [Fact]
    public async Task The_override_without_a_marker_still_calls_slack_com()
    {
        var slack = new SlackStubHandler(static _ => SlackStubHandler.Json(AuthOk));
        var fixtureToken = string.Concat("xo", "xp-fixture-", "0000");   // why: split so no token-shaped literal sits in the source
        using var client = new SlackWebClient(slack, new SlackWebSettings("http://127.0.0.1:47123/api/", Home: "/nonexistent-sindri-home"), _budget, _clock);

        _ = await client.AuthTest(fixtureToken, TestContext.Current.CancellationToken);

        Assert.Equal("slack.com", Assert.Single(slack.Seen).Url.Host);
    }

    private SlackWebClient Client(SlackStubHandler slack) => new(slack, new SlackWebSettings(BaseUrlOverride: null, Home: "/nonexistent-sindri-home"), _budget, _clock);
}
