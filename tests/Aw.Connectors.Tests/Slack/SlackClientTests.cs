using System.Net;
using Aw.Connectors.Core;
using Aw.Connectors.Core.Slack;
using Aw.Connectors.Infrastructure.Slack;
using Aw.Connectors.Testing;
using Aw.Connectors.Testing.GitHub;
using Aw.Connectors.Testing.Slack;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Connectors.Tests.Slack;

public sealed class SlackClientTests : IDisposable
{
    private const string General = "C0001GEN";
    private const string Other = "C0009OTH";
    private const string DirectMessage = "D0003DMX";
    private const string Token = "xo" + "xb-unit-test-token-0123456789";

    private readonly FakeSlackServer _server = new(General);
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 27, 9, 0, 0, TimeSpan.Zero));

    public void Dispose() => _server.Dispose();

    private static DateTimeOffset T => new(2026, 10, 27, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string At(int minutes) => SlackTs.Format(T.AddMinutes(minutes));

    private SlackClient Client(string? token = Token, params string[] channels)
        => new(new HttpClient(_server), new SlackClientSettings { Token = token, Channels = channels.Length == 0 ? [General] : channels }, _clock);

    private static string Decoded(RecordedRequest request) => Uri.UnescapeDataString(request.Uri.AbsoluteUri);

    [Fact, Trait("Lane", "unit")]
    public async Task The_history_request_names_the_channel_and_the_inclusive_oldest_and_carries_the_token_as_a_bearer()
    {
        _ = _server.Add(General, A.SlackMessage(At(1)));

        var result = await Client().History(General, At(1), latest: null, Ct);

        Assert.True(result.IsSuccess);
        var request = Assert.Single(_server.Requests);
        Assert.Equal($"https://slack.com/api/conversations.history?channel={General}&limit=200&inclusive=true&oldest={At(1)}", Decoded(request));
        Assert.Equal($"Bearer {Token}", request.Authorization);
        Assert.Equal(HttpMethod.Get, request.Method);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_bound_is_sent_as_latest()
    {
        _ = await Client().History(General, oldest: null, At(2), Ct);

        Assert.Contains($"latest={At(2)}", Decoded(Assert.Single(_server.Requests)), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_first_read_sends_no_oldest()
    {
        _ = await Client().History(General, oldest: null, latest: null, Ct);

        Assert.All(_server.Requests, static r => Assert.DoesNotContain("oldest", r.Uri.Query, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task History_pages_on_the_cursor_until_the_end_and_comes_back_whole_newest_first()
    {
        for (var i = 1; i <= 5; i++)
        {
            _ = _server.Add(General, A.SlackMessage(At(i)).WithText($"m{i.ToString(System.Globalization.CultureInfo.InvariantCulture)}"));
        }

        _server.MaxPageSize = 2;

        var page = (await Client().History(General, oldest: null, latest: null, Ct)).Value;

        Assert.Equal(["m5", "m4", "m3", "m2", "m1"], page.Items.Select(static m => m.Text), StringComparer.Ordinal);
        Assert.Equal(3, _server.Requests.Count);
        Assert.DoesNotContain("cursor=", _server.Requests[0].Uri.Query, StringComparison.Ordinal);
        Assert.Contains("cursor=off2", _server.Requests[1].Uri.Query, StringComparison.Ordinal);
        Assert.Contains("cursor=off4", _server.Requests[2].Uri.Query, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_thread_is_read_by_its_root_ts_and_pages_too()
    {
        _ = _server.Add(General, A.SlackMessage(At(1)).WithText("root"));
        for (var i = 2; i <= 4; i++)
        {
            _ = _server.Add(General, A.SlackMessage(At(i)).InThread(At(1)).WithText($"r{i.ToString(System.Globalization.CultureInfo.InvariantCulture)}"));
        }

        _server.MaxPageSize = 2;

        var page = (await Client().Replies(General, At(1), Ct)).Value;

        Assert.Equal(["root", "r2", "r3", "r4"], page.Items.Select(static m => m.Text), StringComparer.Ordinal);
        Assert.Equal($"https://slack.com/api/conversations.replies?channel={General}&ts={At(1)}&limit=200", Decoded(_server.Requests[0]));
        Assert.Equal(2, _server.Requests.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_probe_asks_for_one_message()
    {
        _ = _server.Add(General, A.SlackMessage(At(1)));

        var result = await Client().Probe(General, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal($"https://slack.com/api/conversations.history?channel={General}&limit=1", Decoded(Assert.Single(_server.Requests)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_429_comes_back_as_a_value_with_the_reset_time_and_what_was_read_before_it()
    {
        for (var i = 1; i <= 4; i++)
        {
            _ = _server.Add(General, A.SlackMessage(At(i)));
        }

        _server.MaxPageSize = 2;
        _ = _server.ScriptThrough().ScriptRateLimit(30);

        var result = await Client().History(General, oldest: null, latest: null, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(_clock.GetUtcNow().AddSeconds(30), result.Value.RateLimitedUntil);
        Assert.Equal(2, result.Value.Items.Count);
        Assert.Equal(2, _server.Requests.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_429_without_retry_after_waits_a_minute()
    {
        _ = _server.ScriptRateLimit(retryAfterSeconds: null);

        var result = await Client().Replies(General, At(1), Ct);

        Assert.Equal(_clock.GetUtcNow().AddSeconds(60), result.Value.RateLimitedUntil);
        Assert.Empty(result.Value.Items);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_429_on_the_probe_is_the_reset_time()
    {
        _ = _server.ScriptRateLimit(5);

        var result = await Client().Probe(General, Ct);

        Assert.Equal(_clock.GetUtcNow().AddSeconds(5), result.Value);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("invalid_auth", "AW-CONNECTORS-002")]
    [InlineData("token_revoked", "AW-CONNECTORS-002")]
    [InlineData("not_authed", "AW-CONNECTORS-002")]
    [InlineData("missing_scope", "AW-CONNECTORS-007")]
    [InlineData("not_in_channel", "AW-CONNECTORS-007")]
    [InlineData("channel_not_found", "AW-CONNECTORS-008")]
    [InlineData("something_new", "AW-CONNECTORS-006")]
    public async Task An_ok_false_reply_becomes_a_typed_error_that_names_slacks_code(string slackCode, string expected)
    {
        _ = _server.ScriptError(slackCode);

        var error = (await Client().History(General, oldest: null, latest: null, Ct)).Error;

        Assert.Equal(expected, error.Code);
        Assert.Contains(slackCode, error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_redirect_is_refused_and_never_followed()
    {
        _ = _server.ScriptRedirect("https://evil.example.net/api/conversations.history");

        var error = (await Client().History(General, oldest: null, latest: null, Ct)).Error;

        Assert.Equal(ConnectorsErrors.RedirectRefused.Code, error.Code);
        _ = Assert.Single(_server.Requests);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(HttpStatusCode.Unauthorized, "AW-CONNECTORS-002")]
    [InlineData(HttpStatusCode.Forbidden, "AW-CONNECTORS-007")]
    [InlineData(HttpStatusCode.InternalServerError, "AW-CONNECTORS-006")]
    public async Task A_bad_http_status_is_a_typed_error(HttpStatusCode status, string expected)
    {
        _ = _server.ScriptStatus(status);

        Assert.Equal(expected, (await Client().History(General, oldest: null, latest: null, Ct)).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_channel_that_is_not_in_the_allowlist_is_refused_with_no_request_sent()
    {
        var client = Client();

        var error = (await client.History(Other, oldest: null, latest: null, Ct)).Error;

        Assert.Equal("SND-INDEX-013", error.Code);
        Assert.Empty(_server.Requests);
        Assert.Equal("SND-INDEX-013", (await client.Replies(Other, At(1), Ct)).Error.Code);
        Assert.Equal("SND-INDEX-013", (await client.Probe(Other, Ct)).Error.Code);
        Assert.Empty(_server.Requests);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(DirectMessage)]
    [InlineData("d0003dmx")]
    [InlineData("")]
    [InlineData("C0001GEN&channel=C0009OTH")]
    public async Task A_direct_message_or_a_malformed_id_is_refused_even_when_listed(string id)
    {
        var client = Client(Token, General, id);

        Assert.Equal("SND-INDEX-013", (await client.History(id, oldest: null, latest: null, Ct)).Error.Code);
        Assert.Empty(_server.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_thread_ts_that_is_not_a_ts_is_refused_with_no_request_sent()
    {
        var error = (await Client().Replies(General, "1&channel=C0009OTH", Ct)).Error;

        Assert.Equal(ConnectorsErrors.Invalid.Code, error.Code);
        Assert.Empty(_server.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_token_is_refused_with_no_request_sent()
    {
        Assert.Equal(ConnectorsErrors.Unauthorized.Code, (await Client(token: null).History(General, oldest: null, latest: null, Ct)).Error.Code);
        Assert.Empty(_server.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_token_with_whitespace_is_refused_with_no_request_sent_and_is_not_repeated()
    {
        var spaced = Token + " extra";

        var error = (await Client(spaced).History(General, oldest: null, latest: null, Ct)).Error;

        Assert.Equal(ConnectorsErrors.Invalid.Code, error.Code);
        Assert.DoesNotContain(Token, error.Message, StringComparison.Ordinal);
        Assert.Empty(_server.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_error_repeats_the_token_even_when_the_server_reflects_it_or_the_transport_fails_with_it()
    {
        var errors = new List<string>();
        foreach (var script in new Action<FakeSlackServer>[]
        {
            static s => s.ScriptReflectToken(),
            static s => s.ScriptStatus(HttpStatusCode.Forbidden),
            static s => s.ScriptStatus(HttpStatusCode.InternalServerError),
            static s => s.ScriptRedirect("https://evil.example.net/"),
            static s => s.ScriptError("invalid_auth"),
            static s => s.Script(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("not json " + Token) }),
            static s => s.Script(r => throw new HttpRequestException("connect failed for " + r.Headers.Authorization)),
        })
        {
            using var server = new FakeSlackServer(General);
            script(server);
            var result = await new SlackClient(new HttpClient(server), new SlackClientSettings { Token = Token, Channels = [General] }, _clock).History(General, oldest: null, latest: null, Ct);
            if (result.IsError)
            {
                errors.Add(result.Error.Code + " " + result.Error.Message);
            }
        }

        Assert.NotEmpty(errors);
        Assert.All(errors, e => Assert.DoesNotContain(Token, e, StringComparison.Ordinal));
        Assert.All(errors, static e => Assert.DoesNotContain("xo" + "xb-", e, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_transport_failure_is_unreachable_not_a_throw()
    {
        _ = _server.Script(static _ => throw new HttpRequestException("down"));

        Assert.Equal(ConnectorsErrors.Unreachable.Code, (await Client().History(General, oldest: null, latest: null, Ct)).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_message_without_a_ts_is_a_bad_reply()
    {
        _ = _server.Script(static _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"ok\":true,\"messages\":[{\"text\":\"no ts\"}]}") });

        Assert.Equal(ConnectorsErrors.BadReply.Code, (await Client().History(General, oldest: null, latest: null, Ct)).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_author_comes_from_the_display_name_then_the_real_name_then_a_bot_username_and_never_the_user_id()
    {
        _ = _server.Script(static _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"ok\":true,\"messages\":["
                + "{\"ts\":\"1790000003.000100\",\"user\":\"U0000AAAA\",\"text\":\"a\",\"user_profile\":{\"display_name\":\"\",\"real_name\":\"Ada Real\"}},"
                + "{\"ts\":\"1790000002.000100\",\"user\":\"U0000BBBB\",\"text\":\"b\",\"username\":\"deploy-bot\",\"subtype\":\"bot_message\"},"
                + "{\"ts\":\"1790000001.000100\",\"user\":\"U0000CCCC\",\"text\":\"c\"}]}"),
        });

        var items = (await Client().History(General, oldest: null, latest: null, Ct)).Value.Items;

        Assert.Equal(["Ada Real", "deploy-bot", null], items.Select(static m => m.Author), StringComparer.Ordinal);
        Assert.Equal("bot_message", items[1].Subtype);
    }
}
