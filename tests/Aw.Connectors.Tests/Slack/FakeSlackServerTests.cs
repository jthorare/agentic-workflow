using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using Aw.Connectors.Testing.Slack;
using static Aw.Connectors.Testing.Slack.SlackWorkspaceSeed;

namespace Aw.Connectors.Tests.Slack;

/// <summary>
/// The fake Slack's contract for the Messages Release 1 reads (messages spec §5.1 to §5.3, §8.1): each method answers the seeded workspace in Slack's shape,
/// pages on its cursor, and the scripts (a per-method 429 with Retry-After, a revoked token) answer before the data.
/// </summary>
public sealed class FakeSlackServerTests : IDisposable
{
    private const string Token = "xo" + "xp-fixture-" + "0123456789abcdef";

    private readonly FakeSlackServer _server = new FakeSlackServer().SeedWorkspace();
    private readonly HttpClient _client;

    public FakeSlackServerTests()
    {
        _client = new HttpClient(_server, disposeHandler: false);
    }

    public void Dispose()
    {
        _client.Dispose();
        _server.Dispose();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<(HttpResponseMessage Response, JsonNode Body)> Call(string methodAndQuery, HttpContent? form = null)
    {
        using var request = new HttpRequestMessage(form is null ? HttpMethod.Get : HttpMethod.Post, new Uri("https://slack.com/api/" + methodAndQuery)) { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        var response = await _client.SendAsync(request, Ct);
        return (response, JsonNode.Parse(await response.Content.ReadAsStringAsync(Ct))!);
    }

    private static string[] Values(JsonNode body, string list, string field) => [.. body[list]!.AsArray().Select(n => n![field]!.GetValue<string>())];

    private static string[] SearchTs(JsonNode body) => [.. body["messages"]!["matches"]!.AsArray().Select(static n => n!["ts"]!.GetValue<string>())];

    private static string Q(string query) => Uri.EscapeDataString(query);

    [Fact, Trait("Lane", "unit")]
    public async Task Fake_slack_serves_a_dm_and_a_429()
    {
        var (_, list) = await Call("users.conversations?types=im");
        var dm = Assert.Single(list["channels"]!.AsArray())!;
        Assert.Equal(DirectMessage, dm["id"]!.GetValue<string>());
        Assert.Equal(Pat, dm["user"]!.GetValue<string>());

        var (_, history) = await Call("conversations.history?channel=" + DirectMessage);
        Assert.Equal([DmWaitingTs, DmMyAnswerTs, DmAnsweredTs], Values(history, "messages", "ts"), StringComparer.Ordinal);
        Assert.Equal([Pat, Me, Pat], Values(history, "messages", "user"), StringComparer.Ordinal);

        _ = _server.ScriptRateLimit("conversations.history", 30);
        var (limited, body) = await Call("conversations.history?channel=" + DirectMessage);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(30), limited.Headers.RetryAfter?.Delta);
        Assert.Equal("ratelimited", body["error"]!.GetValue<string>());

        var (again, _) = await Call("conversations.history?channel=" + DirectMessage);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);   // why: the 429 was scripted once
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Auth_test_names_the_workspace_the_user_and_the_granted_scopes()
    {
        var (response, body) = await Call("auth.test");

        Assert.True(body["ok"]!.GetValue<bool>());
        Assert.Equal("T0000FIX", body["team_id"]!.GetValue<string>());
        Assert.Equal(Me, body["user_id"]!.GetValue<string>());
        Assert.Equal("https://fixture.slack.com/", body["url"]!.GetValue<string>());
        Assert.Equal(FakeSlackServer.ReleaseOneScopes, Assert.Single(response.Headers.GetValues("x-oauth-scopes")));
        Assert.Equal("Bearer " + Token, Assert.Single(_server.Requests).Authorization);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Users_conversations_lists_the_kinds_its_types_name_and_public_channels_by_default()
    {
        var (_, all) = await Call("users.conversations?types=" + Q("public_channel,private_channel,im,mpim"));
        var (_, plain) = await Call("users.conversations");
        var (_, privateOnly) = await Call("users.conversations?types=private_channel");

        Assert.Equal([DirectMessage, GroupMessage, General, Private], Values(all, "channels", "id"), StringComparer.Ordinal);
        Assert.Equal([General], Values(plain, "channels", "id"), StringComparer.Ordinal);
        var ops = Assert.Single(privateOnly["channels"]!.AsArray())!;
        Assert.True(ops["is_private"]!.GetValue<bool>());
        Assert.True(ops["is_group"]!.GetValue<bool>());
        Assert.Equal("ops-rota", ops["name"]!.GetValue<string>());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Users_conversations_pages_on_the_cursor()
    {
        var types = "&types=" + Q("public_channel,private_channel,im,mpim");

        var (_, first) = await Call("users.conversations?limit=3" + types);
        var cursor = first["response_metadata"]!["next_cursor"]!.GetValue<string>();
        var (_, second) = await Call("users.conversations?limit=3&cursor=" + cursor + types);

        Assert.Equal([DirectMessage, GroupMessage, General], Values(first, "channels", "id"), StringComparer.Ordinal);
        Assert.NotEmpty(cursor);
        Assert.Equal([Private], Values(second, "channels", "id"), StringComparer.Ordinal);
        Assert.Empty(second["response_metadata"]!["next_cursor"]!.GetValue<string>());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Users_conversations_leaves_out_archived_ones_when_asked_and_never_lists_one_i_left()
    {
        _ = _server
            .AddConversation(new SlackConversationSeed("C0003OLD", SlackConversationKind.Public, "old-launch") { IsArchived = true })
            .AddConversation(new SlackConversationSeed("C0004LFT", SlackConversationKind.Public, "left-behind") { IsMember = false });

        var (_, kept) = await Call("users.conversations");
        var (_, hidden) = await Call("users.conversations?exclude_archived=true");

        Assert.Equal([General, "C0003OLD"], Values(kept, "channels", "id"), StringComparer.Ordinal);
        Assert.Equal([General], Values(hidden, "channels", "id"), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Conversations_info_describes_a_dm_with_its_read_state_and_refuses_an_unknown_id()
    {
        var (_, info) = await Call("conversations.info?channel=" + DirectMessage);
        var (_, unknown) = await Call("conversations.info?channel=C9999NOP");

        var channel = info["channel"]!;
        Assert.True(channel["is_im"]!.GetValue<bool>());
        Assert.Equal(Pat, channel["user"]!.GetValue<string>());
        Assert.Equal(DmMyAnswerTs, channel["last_read"]!.GetValue<string>());
        Assert.Equal(1, channel["unread_count"]!.GetValue<int>());
        Assert.False(unknown["ok"]!.GetValue<bool>());
        Assert.Equal("channel_not_found", unknown["error"]!.GetValue<string>());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Conversations_history_shows_a_thread_as_its_root_with_the_reply_count()
    {
        var (_, history) = await Call("conversations.history?channel=" + General);

        Assert.Equal([MentionTs, ThreadRootTs], Values(history, "messages", "ts"), StringComparer.Ordinal);
        var root = history["messages"]![1]!;
        Assert.Equal(2, root["reply_count"]!.GetValue<int>());
        Assert.Equal(ThreadNewReplyTs, root["latest_reply"]!.GetValue<string>());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Conversations_replies_with_oldest_returns_the_root_and_only_the_newer_replies()
    {
        var thread = "conversations.replies?channel=" + General + "&ts=" + ThreadRootTs;

        var (_, whole) = await Call(thread);
        var (_, newer) = await Call(thread + "&oldest=" + ThreadMyReplyTs);
        var (_, inclusive) = await Call(thread + "&oldest=" + ThreadMyReplyTs + "&inclusive=true");

        Assert.Equal([ThreadRootTs, ThreadMyReplyTs, ThreadNewReplyTs], Values(whole, "messages", "ts"), StringComparer.Ordinal);
        Assert.Equal([ThreadRootTs, ThreadNewReplyTs], Values(newer, "messages", "ts"), StringComparer.Ordinal);
        Assert.Equal([ThreadRootTs, ThreadMyReplyTs, ThreadNewReplyTs], Values(inclusive, "messages", "ts"), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Users_info_answers_a_known_person_and_user_not_found()
    {
        var (_, pat) = await Call("users.info?user=" + Pat);
        var (_, bot) = await Call("users.info?user=" + Bot);
        var (_, unknown) = await Call("users.info?user=U9999NOP");

        Assert.Equal("Pat Fixture", pat["user"]!["profile"]!["display_name"]!.GetValue<string>());
        Assert.Equal("T0000FIX", pat["user"]!["team_id"]!.GetValue<string>());
        Assert.True(bot["user"]!["is_bot"]!.GetValue<bool>());
        Assert.Equal("user_not_found", unknown["error"]!.GetValue<string>());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Emoji_list_returns_the_custom_emoji_and_its_alias()
    {
        var (_, body) = await Call("emoji.list");

        var emoji = body["emoji"]!.AsObject();
        Assert.Equal(["fixture-ok", "fixture-yes"], emoji.Select(static e => e.Key), StringComparer.Ordinal);
        Assert.StartsWith("https://", emoji["fixture-ok"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal("alias:fixture-ok", emoji["fixture-yes"]!.GetValue<string>());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Search_messages_finds_what_is_to_me_and_from_me_newest_first()
    {
        var (_, to) = await Call("search.messages?query=" + Q("to:me"));
        var (_, from) = await Call("search.messages?query=" + Q("from:me"));

        Assert.Equal([DmWaitingTs, GroupTs, MentionTs, DmAnsweredTs], SearchTs(to), StringComparer.Ordinal);
        Assert.Equal(4, to["messages"]!["total"]!.GetValue<int>());
        Assert.Equal(DirectMessage, to["messages"]!["matches"]![0]!["channel"]!["id"]!.GetValue<string>());
        Assert.Equal([ThreadMyReplyTs, DmMyAnswerTs], SearchTs(from), StringComparer.Ordinal);
        Assert.Contains("?thread_ts=" + ThreadRootTs, from["messages"]!["matches"]![0]!["permalink"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Search_messages_matches_words_ignoring_case_inside_one_channel()
    {
        var (_, body) = await Call("search.messages?query=" + Q("Changelog in:general"));

        Assert.Equal([ThreadMyReplyTs, ThreadRootTs], SearchTs(body), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Search_messages_pages_by_count_and_page()
    {
        var (_, body) = await Call("search.messages?query=" + Q("to:me") + "&count=3&page=2");

        Assert.Equal([DmAnsweredTs], SearchTs(body), StringComparer.Ordinal);
        var paging = body["messages"]!["paging"]!;
        Assert.Equal(2, paging["page"]!.GetValue<int>());
        Assert.Equal(2, paging["pages"]!.GetValue<int>());
        Assert.Equal(4, paging["total"]!.GetValue<int>());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_search_modifier_the_fake_does_not_know_fails_the_run()
    {
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => Call("search.messages?query=" + Q("has:reaction")));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rate_limit_scripted_for_one_method_leaves_the_other_methods_alone()
    {
        _ = _server.ScriptRateLimit("search.messages", 5, times: 2);
        var search = "search.messages?query=" + Q("to:me");

        var (info, _) = await Call("users.info?user=" + Pat);
        var (first, _) = await Call(search);
        var (history, _) = await Call("conversations.history?channel=" + DirectMessage);
        var (second, _) = await Call(search);
        var (third, _) = await Call(search);

        Assert.Equal(HttpStatusCode.OK, info.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, first.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(5), first.Headers.RetryAfter?.Delta);
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.Equal(HttpStatusCode.OK, third.StatusCode);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_rate_limit_for_a_method_the_fake_does_not_serve_is_refused()
    {
        _ = Assert.Throws<ArgumentException>(() => _server.ScriptRateLimit("chat.postMessage", 5));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_revoked_token_answers_invalid_auth_on_every_method()
    {
        _ = _server.Revoke();
        string[] calls =
        [
            "auth.test",
            "users.conversations",
            "conversations.history?channel=" + DirectMessage,
            "conversations.replies?channel=" + General + "&ts=" + ThreadRootTs,
            "conversations.info?channel=" + DirectMessage,
            "users.info?user=" + Pat,
            "emoji.list",
            "search.messages?query=" + Q("to:me"),
        ];

        foreach (var call in calls)
        {
            var (response, body) = await Call(call);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.False(body["ok"]!.GetValue<bool>());
            Assert.Equal("invalid_auth", body["error"]!.GetValue<string>());
        }
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_revoked_token_answers_before_a_scripted_429()
    {
        _ = _server.ScriptRateLimit("auth.test", 5).Revoke();

        var (response, body) = await Call("auth.test");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("invalid_auth", body["error"]!.GetValue<string>());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_form_body_is_read_like_a_query()
    {
        using var form = new FormUrlEncodedContent([new KeyValuePair<string?, string?>("user", Pat)]);

        var (_, body) = await Call("users.info", form);

        Assert.Equal(Pat, body["user"]!["id"]!.GetValue<string>());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_method_the_fake_does_not_serve_still_fails_the_run()
    {
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => Call("chat.postMessage"));
    }
}
