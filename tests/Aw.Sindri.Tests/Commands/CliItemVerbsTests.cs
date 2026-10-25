using System.Net;
using System.Text;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.CreateItem;
using Aw.Sindri.Features.GetItem;
using Aw.Sindri.Features.GetQueue;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Features.ListItems;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Commands;

/// <summary>The item verbs (API P3 unit H): queue, items, item add, item close.</summary>
public sealed class CliItemVerbsTests : IDisposable
{
    private static readonly FakeControlPlaneProbe _probe = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private static readonly AsOf _fresh = new(5, 5, KnownInstants.Epoch, Rebuilding: false, Stalled: false);
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static IHumanTokenStore Human => InMemoryHumanTokenStore.WithToken();

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_queue_prints_each_group_and_its_empty_text()
    {
        var item = Summary(A.FirstItemId, "Try the queue", ItemState.New);
        var queue = new QueueResponse(
            [new(QueueGroup.WaitingOnYou, [], EmptyText.Group(QueueGroup.WaitingOnYou)), new(QueueGroup.Queued, [item], Empty: null)],
            Empty: null, _fresh);
        var api = new RoutedApi().On("/sindri/queue", HttpStatusCode.OK, queue);

        var code = await Verbs(api, Human).Run(["queue"], Token);

        Assert.Equal(0, code);
        Assert.Equal($"waitingOnYou\n  Nothing is waiting on you.\nqueued\n  {A.FirstItemId.Value}  Try the queue  [demo]  new  since 2026-01-01T00:00:00Z\n", _out.ToString().ReplaceLineEndings("\n"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_queue_exits_1_while_stalled()
    {
        var stalled = new AsOf(2, 9, KnownInstants.Epoch, Rebuilding: false, Stalled: true);
        var api = new RoutedApi().On("/sindri/queue", HttpStatusCode.OK, new QueueResponse([], EmptyText.Stalled(3), stalled));

        var code = await Verbs(api, Human).Run(["queue"], Token);

        Assert.Equal(1, code);
        Assert.Contains(EmptyText.Stalled(3), _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_queue_json_is_the_raw_body()
    {
        var queue = new QueueResponse([], EmptyText.Queue(), _fresh);
        var api = new RoutedApi().On("/sindri/queue", HttpStatusCode.OK, queue);

        var code = await Verbs(api, Human).Run(["queue", "--json"], Token);

        Assert.Equal((0, Encoding.UTF8.GetString(AwJson.Serialize(queue, SliceHarness.Json)) + "\n"), (code, _out.ToString().ReplaceLineEndings("\n")));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_item_add_posts_as_human()
    {
        var created = new ItemCreatedResponse(new CommandAccepted(new SequentialIdGenerator().New<CommandId>(), Sequence: 4), A.FirstItemId);
        var api = new RoutedApi().On("/sindri/items", HttpStatusCode.OK, created);

        var code = await Verbs(api, Human).Run(["item", "add", "Try", "the", "queue", "--repo", "demo"], Token);

        Assert.Equal((0, $"{A.FirstItemId.Value} created (sequence 4)\n"), (code, _out.ToString().ReplaceLineEndings("\n")));
        var call = Assert.Single(api.Calls);
        Assert.Equal(("POST", "human", """{"title":"Try the queue","repo":"demo","body":null,"link":null}"""), (call.Method, call.Headers[SindriHeaders.Caller], call.Body));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_item_close_reads_the_version_when_none_given()
    {
        var api = new RoutedApi().On("/sindri/status", HttpStatusCode.OK, RoutedApi.StatusAtHead7).On("/sindri/items/" + A.FirstItemId.Value, HttpStatusCode.OK, View(version: 3))
            .On("/sindri/items/" + A.FirstItemId.Value + "/close", HttpStatusCode.OK, new CommandAccepted(new SequentialIdGenerator().New<CommandId>(), Sequence: 9));

        var code = await Verbs(api, Human).Run(["item", "close", A.FirstItemId.Value], Token);

        Assert.Equal((0, $"{A.FirstItemId.Value} closed: done (sequence 9)\n"), (code, _out.ToString().ReplaceLineEndings("\n")));
        Assert.Equal("""{"reason":"done","itemVersion":3}""", api.Calls[^1].Body);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_item_close_pre_read_waits_for_the_ledger_head_read_from_status()
    {
        var api = new RoutedApi().On("/sindri/status", HttpStatusCode.OK, RoutedApi.StatusAtHead7)
            .On("/sindri/items/" + A.FirstItemId.Value, HttpStatusCode.OK, View(version: 3))
            .On("/sindri/items/" + A.FirstItemId.Value + "/close", HttpStatusCode.OK, new CommandAccepted(new SequentialIdGenerator().New<CommandId>(), Sequence: 9));

        var code = await Verbs(api, Human).Run(["item", "close", A.FirstItemId.Value], Token);

        Assert.Equal(0, code);
        Assert.Equal([("GET", "/sindri/status", null), ("GET", "/sindri/items/" + A.FirstItemId.Value, "7"), ("POST", "/sindri/items/" + A.FirstItemId.Value + "/close", null)],
            api.Calls.Select(static c => (c.Method, c.Path, c.Headers.GetValueOrDefault(SindriHeaders.After))));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_items_and_queue_send_the_ledger_head_as_x_sindri_after()
    {
        var api = new RoutedApi().On("/sindri/status", HttpStatusCode.OK, RoutedApi.StatusAtHead7)
            .On("/sindri/items", HttpStatusCode.OK, new ListItemsResponse([], EmptyText.Items(state: null, repo: null), _fresh))
            .On("/sindri/queue", HttpStatusCode.OK, new QueueResponse([], EmptyText.Queue(), _fresh));

        _ = await Verbs(api, Human).Run(["items"], Token);
        _ = await Verbs(api, Human).Run(["queue"], Token);

        Assert.Equal("7,7", string.Join(',', api.Calls.Where(static c => !string.Equals(c.Path, "/sindri/status", StringComparison.Ordinal)).Select(static c => c.Headers.GetValueOrDefault(SindriHeaders.After))));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_items_prints_the_servers_message_and_exits_2_when_the_views_stay_behind()
    {
        var api = new RoutedApi().On("/sindri/status", HttpStatusCode.OK, RoutedApi.StatusAtHead7)
            .OnRaw("/sindri/items", HttpStatusCode.ServiceUnavailable, """{"type":"about:blank","title":"SND-API-011","status":503,"detail":"views at cursor 3, waiting for 7"}""");

        var code = await Verbs(api, Human).Run(["items"], Token);

        Assert.Equal((2, "SND-API-011 views at cursor 3, waiting for 7\n"), (code, _error.ToString().ReplaceLineEndings("\n")));
    }

    private static GetStatusResponse StatusWith(ViewsStatus views) => RoutedApi.StatusAtHead7 with { Views = views };

    private static ViewsStatus Behind(bool rebuilding, bool stalled) =>
        new(Cursor: 3, LedgerHead: 7, ProjectedAt: KnownInstants.Epoch, Rebuilding: rebuilding, Stalled: stalled, StalledAt: stalled ? 3 : null, StalledType: null);

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_queue_while_the_views_are_stalled_skips_the_header_and_exits_1_with_the_sentence()
    {
        var stalled = new AsOf(3, 7, KnownInstants.Epoch, Rebuilding: false, Stalled: true);
        var api = new RoutedApi().HonouringAfter(cursor: 3).On("/sindri/status", HttpStatusCode.OK, StatusWith(Behind(rebuilding: false, stalled: true)))
            .On("/sindri/queue", HttpStatusCode.OK, new QueueResponse([], EmptyText.Stalled(3), stalled));

        var code = await Verbs(api, Human).Run(["queue"], Token);

        Assert.Equal((1, null), (code, api.Calls[^1].Headers.GetValueOrDefault(SindriHeaders.After)));
        Assert.Contains(EmptyText.Stalled(3), _out.ToString(), StringComparison.Ordinal);
        Assert.Empty(_error.ToString());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_items_while_the_views_rebuild_skips_the_header_and_exits_1_with_the_sentence()
    {
        var rebuilding = new AsOf(3, 7, KnownInstants.Epoch, Rebuilding: true, Stalled: false);
        var api = new RoutedApi().HonouringAfter(cursor: 3).On("/sindri/status", HttpStatusCode.OK, StatusWith(Behind(rebuilding: true, stalled: false)))
            .On("/sindri/items", HttpStatusCode.OK, new ListItemsResponse([], EmptyText.Rebuilding(3, 7), rebuilding));

        var code = await Verbs(api, Human).Run(["items"], Token);

        Assert.Equal((1, null), (code, api.Calls[^1].Headers.GetValueOrDefault(SindriHeaders.After)));
        Assert.Contains(EmptyText.Rebuilding(3, 7), _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_items_while_stalled_with_rows_prints_the_sentence_before_the_rows()
    {
        var stalled = new AsOf(3, 7, KnownInstants.Epoch, Rebuilding: false, Stalled: true);
        var api = new RoutedApi().On("/sindri/items", HttpStatusCode.OK, new ListItemsResponse([Summary(A.FirstItemId, "Partial", ItemState.New)], EmptyText.Stalled(3), stalled));

        var code = await Verbs(api, Human).Run(["items"], Token);

        var text = _out.ToString().ReplaceLineEndings("\n");
        Assert.Equal(1, code);
        Assert.StartsWith(EmptyText.Stalled(3) + "\n" + A.FirstItemId.Value + "  Partial", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_queue_while_stalled_says_the_sentence_once()
    {
        var stalled = new AsOf(3, 7, KnownInstants.Epoch, Rebuilding: false, Stalled: true);
        var sentence = EmptyText.Stalled(3);
        var queue = new QueueResponse([new(QueueGroup.WaitingOnYou, [], sentence), new(QueueGroup.Queued, [], sentence)], sentence, stalled);
        var api = new RoutedApi().On("/sindri/queue", HttpStatusCode.OK, queue);

        _ = await Verbs(api, Human).Run(["queue"], Token);

        var text = _out.ToString();
        Assert.Equal(1, text.Split(sentence).Length - 1);
        Assert.Contains("waitingOnYou", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_items_json_while_stalled_prints_the_body_and_exits_1()
    {
        var stalled = new AsOf(3, 7, KnownInstants.Epoch, Rebuilding: false, Stalled: true);
        var list = new ListItemsResponse([], EmptyText.Stalled(3), stalled);
        var api = new RoutedApi().HonouringAfter(cursor: 3).On("/sindri/status", HttpStatusCode.OK, StatusWith(Behind(rebuilding: false, stalled: true)))
            .On("/sindri/items", HttpStatusCode.OK, list);

        var code = await Verbs(api, Human).Run(["items", "--json"], Token);

        Assert.Equal((1, Encoding.UTF8.GetString(AwJson.Serialize(list, SliceHarness.Json)) + "\n"), (code, _out.ToString().ReplaceLineEndings("\n")));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_queue_caught_up_sends_the_header_to_a_server_that_honours_it()
    {
        var api = new RoutedApi().HonouringAfter(cursor: 7).On("/sindri/queue", HttpStatusCode.OK, new QueueResponse([], EmptyText.Queue(), _fresh));

        var code = await Verbs(api, Human).Run(["queue"], Token);

        Assert.Equal((0, "7"), (code, api.Calls[^1].Headers.GetValueOrDefault(SindriHeaders.After)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_item_close_cancelled_posts_the_cancelled_variant()
    {
        var api = new RoutedApi().On("/sindri/items/ENG-142/close", HttpStatusCode.OK, new CommandAccepted(new SequentialIdGenerator().New<CommandId>(), Sequence: 12));

        var code = await Verbs(api, Human).Run(["item", "close", "ENG-142", "--cancelled", "--version", "2"], Token);

        Assert.Equal((0, "ENG-142 closed: cancelled (sequence 12)\n"), (code, _out.ToString().ReplaceLineEndings("\n")));
        Assert.Equal("""{"reason":"cancelled","itemVersion":2}""", Assert.Single(api.Calls).Body);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_items_filters()
    {
        var api = new RoutedApi().On("/sindri/items", HttpStatusCode.OK, new ListItemsResponse([], EmptyText.Items(ItemState.Done, RepoName.From("demo")), _fresh));

        var code = await Verbs(api, Human).Run(["items", "--state", "done", "--repo", "demo"], Token);

        Assert.Equal((0, "No done items in demo.\n"), (code, _out.ToString().ReplaceLineEndings("\n")));
        Assert.Equal("?state=done&repo=demo", api.Calls[^1].Query);
    }

    [Fact, Trait("Lane", "unit")]
    public void Usage_and_the_catalog_name_the_same_verbs()
    {
        var forms = new[] { "sindri queue [--json] [--no-start]", "sindri items [--state <s>] [--repo <r>] [--json] [--no-start]", "sindri item add <title> [--repo <r>] [--no-start]", "sindri item close <item> [--cancelled] [--version <n>] [--no-start]" };

        Assert.All(forms, f => Assert.Contains(f, Usage.Text, StringComparison.Ordinal));
    }

    private static ItemSummary Summary(ItemId id, string title, ItemState state) =>
        new(id, title, RepoName.From("demo"), state, Step: null, KnownInstants.Epoch, PrimaryAction: null, Actions: [], Owner: null);

    private static ItemView View(long version) =>
        new(A.FirstItemId, "Try the queue", Repo: null, ItemState.New, Step: null, KnownInstants.Epoch, PrimaryAction: null, Actions: [], Owner: null,
            version, "human:desk", WaitingOn: null, Instance: null, KnownInstants.Epoch, KnownInstants.Epoch, Body: null, Link: null, new Core.Launches.ItemLaunch(Modes: [], Core.Launch.AgentId.From("claude-code"), ChosenBy: "builtIn", Attach: null, Resumable: null, InFlight: null, LastStarted: null, Confirm: null, RepoPath: null, Blockers: []), _fresh);

    private ItemVerbs Verbs(RoutedApi api, IHumanTokenStore tokens)
    {
        var client = new ControlPlaneClient(_probe, SliceHarness.Json,
            CallerHeaders.For(new AgentSession(Terminal: true, Prover: false), tokens, _ => ValueTask.FromResult((RepoName?)null)), handler: api);
        return new ItemVerbs(client, new Output(_out, _error, SliceHarness.Json), (_, _) => Task.FromResult<Kernel.Error?>(null));
    }
}
