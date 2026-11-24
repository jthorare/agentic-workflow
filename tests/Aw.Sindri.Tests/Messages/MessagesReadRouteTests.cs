using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Messages;
using Aw.Sindri.Tests.ApiGoldens;
using Aw.Sindri.Tests.Callers;
using static Aw.Sindri.Tests.Callers.CallerRequests;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Messages.MessageSeed;

namespace Aw.Sindri.Tests.Messages;

/// <summary>
/// M1f (messages spec §8.2, W17): the read routes serve the store's lists, conversations, windows and threads, and a session's read carries keys, kinds and
/// counts, never text, names or topics. The goldens of the same routes are <see cref="MessagesApiGoldenTests"/>.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class MessagesReadRouteTests
{
    // Text and names the seed holds; none of it may reach a session.
    private static readonly string[] _text = ["can you ship the fare table today?", "review the fare table PR?", "and the group path too", "lunch Friday", "Sam Lee", "#harbor", "harbor"];

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public static TheoryData<string, string> ListRows()
    {
        var rows = new TheoryData<string, string>();
        foreach (var (slug, scenario) in MessageScenarios.Lists)
        {
            rows.Add(slug, scenario);
        }

        return rows;
    }

    // The list rows of the Messages golden group (MessagesApiGoldenTests runs the rest).
    [Theory(Timeout = 30_000)]
    [MemberData(nameof(ListRows))]
    public Task ApiGoldens_messages_lists(string slug, string scenario) => ApiGoldenRows.Match(slug, scenario, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Session_read_returns_no_text()
    {
        await using var host = await Seeded();

        var answer = await Read(host, "/sindri/messages/lists/needs-reply", session: true);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Contains("\"rows\"", answer.Body, StringComparison.Ordinal);
        Assert.Equal([SamKey, HarborKey], Keys(answer), StringComparer.Ordinal);
        Assert.All(JsonNode.Parse(answer.Body)!["rows"]!.AsArray(), r => Assert.Equal(string.Empty, (string)r!["newestText"]!));
        Assert.All(_text, t => Assert.DoesNotContain(t, answer.Body, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("/sindri/messages/lists/unread")]
    [InlineData("/sindri/messages/conversations")]
    [InlineData("/sindri/messages/conversations/C0HARBOR/messages")]
    [InlineData("/sindri/messages/conversations/D0SAM/messages")]
    [InlineData("/sindri/messages/threads/C0HARBOR/1767771000.000100")]
    public async Task Session_reads_of_every_route_carry_no_text_or_names(string path)
    {
        await using var host = await Seeded();

        var owner = await Read(host, path);
        var session = await Read(host, path, session: true);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (owner.Status, session.Status));
        Assert.Contains(_text, t => owner.Body.Contains(t, StringComparison.Ordinal));   // the owner's answer holds text, so the session check is not empty
        Assert.All(_text, t => Assert.DoesNotContain(t, session.Body, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Needs_reply_lists_the_dm_and_the_thread_that_names_me_oldest_first()
    {
        await using var host = await Seeded();

        var answer = await Read(host, "/sindri/messages/lists/needs-reply");
        var body = JsonNode.Parse(answer.Body)!;

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal([SamKey, HarborKey], Keys(answer), StringComparer.Ordinal);
        Assert.Equal(2, (int)body["count"]!);
        Assert.Null(body["next"]);
        Assert.Equal("can you ship the fare table today?", (string)body["rows"]![0]!["newestText"]!);
    }

    [Fact]
    public async Task Needs_reply_holds_class_3_rows_only_and_leaves_an_fyi_mention_in_unread()
    {
        await using var host = await Seeded();
        _ = await host.App.Services.GetRequiredService<IMessagesStore>().PutMessages(
            [new StoredMessage(Harbor, new MessageTs(FerryNote), ThreadTs: null, "U0ADA", "<@U0JOI> fyi the ferry moved", Mine: false, NamesMe: true)], Cancel);

        var needsReply = await Read(host, "/sindri/messages/lists/needs-reply");
        var unread = JsonNode.Parse((await Read(host, "/sindri/messages/lists/unread")).Body)!["rows"]!.AsArray();

        Assert.Equal([SamKey, HarborKey], Keys(needsReply), StringComparer.Ordinal);
        Assert.All(JsonNode.Parse(needsReply.Body)!["rows"]!.AsArray(), r => Assert.Equal("directQuestion", (string)r!["class"]!));
        Assert.Equal("fyi", (string)unread.Single(r => string.Equals((string)r!["key"]!, FerryKey, StringComparison.Ordinal))!["class"]!);
    }

    [Fact]
    public async Task A_list_pages_after_a_key_and_names_the_next_one()
    {
        await using var host = await Seeded();

        var first = await Read(host, "/sindri/messages/lists/unread?limit=1");
        var second = await Read(host, $"/sindri/messages/lists/unread?limit=2&after={SamKey}");
        var stale = await Read(host, "/sindri/messages/lists/unread?after=slack:dm:T0HARBOR/D0GONE");

        Assert.Equal([SamKey], Keys(first), StringComparer.Ordinal);
        Assert.Equal((3, SamKey), ((int)JsonNode.Parse(first.Body)!["count"]!, (string?)JsonNode.Parse(first.Body)!["next"]));
        Assert.Equal([HarborKey, LunchKey], Keys(second), StringComparer.Ordinal);
        Assert.Null(JsonNode.Parse(second.Body)!["next"]);
        Assert.Equal((HttpStatusCode.BadRequest, "SND-API-013"), (stale.Status, stale.Code));
    }

    [Fact]
    public async Task A_list_with_no_messages_db_is_empty()
    {
        await using var host = await GoldenHost.Start(temporary: true, Cancel);

        var answer = await Read(host, "/sindri/messages/lists/needs-reply");

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Empty(Keys(answer));
        Assert.False(File.Exists(Path.Combine(host.Home, "messages.db")));
    }

    [Fact]
    public async Task Conversations_list_starred_first_with_unread_counts_and_filter_by_name()
    {
        await using var host = await Seeded();

        var all = JsonNode.Parse((await Read(host, "/sindri/messages/conversations")).Body)!["conversations"]!.AsArray();
        var named = await Read(host, "/sindri/messages/conversations?q=HARB");
        var starred = await Read(host, "/sindri/messages/conversations?starred=true");

        Assert.Equal([(Sam, true, 1), (Harbor, false, 2)], all.Select(c => ((string)c!["conversation"]!, (bool)c["starred"]!, (int)c["unread"]!)));
        Assert.Equal([Harbor], Conversations(named), StringComparer.Ordinal);
        Assert.Equal([Sam], Conversations(starred), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_conversation_window_holds_top_level_lines_and_the_gap_between_them()
    {
        await using var host = await Seeded();

        var answer = await Read(host, "/sindri/messages/conversations/C0HARBOR/messages");
        var older = await Read(host, $"/sindri/messages/conversations/C0HARBOR/messages?before={LeeLunch}");
        var unknown = await Read(host, "/sindri/messages/conversations/C0NOWHERE/messages");
        var body = JsonNode.Parse(answer.Body)!;

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal([AdaRoot, LeeLunch], Ts(body["lines"]!), StringComparer.Ordinal);
        Assert.Equal(1, (int)body["lines"]![0]!["replies"]!);
        Assert.Equal(12, (int)Assert.Single(body["gaps"]!.AsArray())!["missing"]!);
        Assert.Equal([AdaRoot], Ts(JsonNode.Parse(older.Body)!["lines"]!), StringComparer.Ordinal);
        _ = Assert.Single(JsonNode.Parse(older.Body)!["gaps"]!.AsArray());   // the hole starts right after the window's newest line, so its marker shows
        Assert.Equal((HttpStatusCode.NotFound, "SND-MSG-011"), (unknown.Status, unknown.Code));
    }

    [Fact]
    public async Task A_dm_window_names_the_author_from_the_users_table()
    {
        await using var host = await Seeded();

        var line = JsonNode.Parse((await Read(host, "/sindri/messages/conversations/D0SAM/messages")).Body)!["lines"]![0]!;

        Assert.Equal(("U0SAM", "Sam Lee"), ((string)line["author"]!, (string?)line["authorName"]));
    }

    [Fact]
    public async Task A_thread_holds_its_root_its_replies_and_its_needs_reply_row()
    {
        await using var host = await Seeded();

        var answer = await Read(host, $"/sindri/messages/threads/C0HARBOR/{AdaRoot}");
        var quiet = await Read(host, $"/sindri/messages/threads/C0HARBOR/{LeeLunch}");
        var missing = await Read(host, "/sindri/messages/threads/C0HARBOR/1767779999.000100");
        var body = JsonNode.Parse(answer.Body)!;

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(AdaRoot, (string)body["root"]!["ts"]!);
        Assert.Equal([KimReply], Ts(body["replies"]!), StringComparer.Ordinal);
        Assert.Equal(HarborKey, (string)body["row"]!["key"]!);
        Assert.Equal(LunchKey, (string)JsonNode.Parse(quiet.Body)!["row"]!["key"]!);   // not mine, but unread
        Assert.Equal((HttpStatusCode.NotFound, "SND-MSG-011"), (missing.Status, missing.Code));
    }

    private static async Task<GoldenHost> Seeded()
    {
        var host = await GoldenHost.Start(temporary: true, Cancel);
        await Seed(host.App.Services, host.Home, Cancel);
        return host;
    }

    private static async Task<Answer> Read(GoldenHost host, string path, bool session = false)
    {
        if (!session)
        {
            return await host.Http.Ask(Get(path, Human(host.Token.Value)));
        }

        using (AsSession())   // why: Human reads the scope when it builds the headers
        {
            return await host.Http.Ask(Get(path, Human(host.Token.Value)));
        }
    }

    private static List<string> Keys(Answer answer) => [.. JsonNode.Parse(answer.Body)!["rows"]!.AsArray().Select(r => (string)r!["key"]!)];

    private static List<string> Conversations(Answer answer) => [.. JsonNode.Parse(answer.Body)!["conversations"]!.AsArray().Select(c => (string)c!["conversation"]!)];

    private static List<string> Ts(JsonNode lines) => [.. lines.AsArray().Select(l => (string)l!["ts"]!)];
}
