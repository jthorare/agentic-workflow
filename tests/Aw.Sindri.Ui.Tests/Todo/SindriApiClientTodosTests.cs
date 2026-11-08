using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Todos;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Todo;

/// <summary>The client's todo calls (todo spec §4.4): the routes, the bodies, the list the golden carries and the accepted write.</summary>
[Lane(TestLane.Unit)]
public sealed class SindriApiClientTodosTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private const string Accepted = """{"accepted":{"commandId":"cmd_1","sequence":41},"id":"td_x","version":3,"due":"2026-01-02"}""";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SindriApiClient Client(HttpMessageHandler handler) => new(new HttpClient(handler), new InMemoryHumanToken("tok-1"), TimeProvider.System);

    [Fact]
    public void The_routes_escape_the_id_and_name_the_state()
    {
        Assert.Equal("/sindri/todos?state=all", TodoRoutes.List("all"));
        Assert.Equal("/sindri/todos", TodoRoutes.Add);
        Assert.Equal(
            ["/sindri/todos/td_x/change", "/sindri/todos/td_x/done", "/sindri/todos/td_x/reopen", "/sindri/todos/td_x/remove", "/sindri/todos/td_x/restore"],
            [TodoRoutes.Change("td_x"), TodoRoutes.Done("td_x"), TodoRoutes.Reopen("td_x"), TodoRoutes.Remove("td_x"), TodoRoutes.Restore("td_x")],
            StringComparer.Ordinal);
        Assert.Equal("/sindri/todos/td_a%2Fb", TodoRoutes.One("td_a/b"));
    }

    [Fact]
    public async Task Todos_calls_the_list_route_with_the_state()
    {
        var canned = new Canned(HttpStatusCode.OK, GoldenFiles.Api("todos", "list"));

        var list = await Client(canned).Todos(_api, "all", Token);

        Assert.True(list.IsSuccess);
        var seen = Assert.Single(canned.Seen);
        Assert.Equal((HttpMethod.Get, "/sindri/todos", "?state=all"), (seen.Method, seen.Uri.AbsolutePath, seen.Uri.Query));
    }

    [Fact]
    public async Task The_list_golden_reads_every_field_of_every_todo()
    {
        var golden = GoldenFiles.Node("todos", "list");
        var list = (await Client(new Canned(HttpStatusCode.OK, GoldenFiles.Api("todos", "list"))).Todos(_api, "all", Token)).Value;

        Assert.Equal(("all", 5, 1, 1), (list.State, list.Counts.Open, list.Counts.Done, list.Counts.Removed));
        Assert.Equal((9, 9, false, false), (list.AsOf.Cursor, list.AsOf.LedgerHead, list.AsOf.Rebuilding, list.AsOf.Stalled));
        Assert.Equal(DateTimeOffset.Parse("2026-01-01T00:00:00Z", CultureInfo.InvariantCulture), list.AsOf.ProjectedAt);
        var nodes = golden["todos"]!.AsArray();
        Assert.Equal(nodes.Count, list.Todos.Count);
        for (var i = 0; i < nodes.Count; i++)
        {
            var (n, t) = (nodes[i]!, list.Todos[i]);
            Assert.Equal(n["id"]!.GetValue<string>(), t.Id);
            Assert.Equal(n["text"]!.GetValue<string>(), t.Text);
            Assert.Equal(Day(n["due"]), t.Due);
            Assert.Equal(n["addedAt"]!.GetValue<string>(), t.AddedAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture));
            Assert.Equal(n["addedBy"]!.GetValue<string>(), t.AddedBy);
            Assert.Equal(n["addedVia"]?.GetValue<string>(), t.AddedVia);
            Assert.Equal(Moment(n["doneAt"]), t.DoneAt);
            Assert.Equal(n["removed"]!.GetValue<bool>(), t.Removed);
            Assert.Equal(Moment(n["removedAt"]), t.RemovedAt);
            Assert.Equal(n["removedBy"]?.GetValue<string>(), t.RemovedBy);
            Assert.Equal(n["version"]!.GetValue<long>(), t.Version);
            Assert.Equal(n["lastBy"]!.GetValue<string>(), t.LastBy);
            Assert.Equal(n["lastVia"]?.GetValue<string>(), t.LastVia);
            Assert.Null(t.Conflict);
            var links = n["links"]!.AsArray();
            Assert.Equal(links.Count, t.Links.Count);
            for (var l = 0; l < links.Count; l++)
            {
                Assert.Equal((links[l]!["kind"]!.GetValue<string>(), links[l]!["ref"]!.GetValue<string>(), links[l]!["href"]!.GetValue<string>()), (t.Links[l].Kind, t.Links[l].Ref, t.Links[l].Href));
            }
        }

        var rotate = list.Todos[2];
        Assert.Equal(("rotate the staging key", new DateOnly(2025, 12, 31), "ENG-12", "sindri://linear/ENG-12"), (rotate.Text, rotate.Due, rotate.Links[1].Ref, rotate.Links[1].Href));
        Assert.Equal(("human:cli", "pipe", "session:mcp"), (list.Todos[5].AddedBy, list.Todos[5].AddedVia, list.Todos[5].LastBy));
        Assert.True(list.Todos[6].Removed);
    }

    [Fact]
    public async Task An_open_conflict_reads_both_sides_and_the_note_hash()
    {
        var golden = GoldenFiles.Node("todos", "list");
        golden["todos"]![1]!["conflict"] = JsonNode.Parse("""{"sindri":{"text":"reply to Sam","due":"2026-01-02","done":false},"obsidian":{"text":"reply to Sam today","due":null,"done":true},"noteHash":"sha256:ab12"}""");

        var list = (await Client(new Canned(HttpStatusCode.OK, golden.ToJsonString())).Todos(_api, "all", Token)).Value;

        var conflict = list.Todos[1].Conflict!;
        Assert.Equal(("reply to Sam", new DateOnly(2026, 1, 2), false), (conflict.Sindri.Text, conflict.Sindri.Due, conflict.Sindri.Done));
        Assert.Equal(("reply to Sam today", null, true, "sha256:ab12"), (conflict.Obsidian.Text, conflict.Obsidian.Due, conflict.Obsidian.Done, conflict.NoteHash));
    }

    [Fact]
    public async Task AddTodo_posts_the_text_day_links_and_client_id()
    {
        var canned = new Canned(HttpStatusCode.OK, Accepted);

        var added = await Client(canned).AddTodo(_api, new TodoAdd("call mon", "2026-01-02", ["ENG-1"], "c1"), Token);

        var seen = Assert.Single(canned.Seen);
        Assert.Equal((HttpMethod.Post, "/sindri/todos"), (seen.Method, seen.Uri.AbsolutePath));
        Assert.Equal("""{"text":"call mon","due":"2026-01-02","links":["ENG-1"],"clientId":"c1"}""", Compact(seen.Body));
        Assert.Equal(("td_x", 3, new DateOnly(2026, 1, 2), 41), (added.Value.Id, added.Value.Version, added.Value.Due, added.Value.Accepted.Sequence));
    }

    [Fact]
    public async Task AddTodo_leaves_out_a_day_and_links_it_has_none_of()
    {
        var canned = new Canned(HttpStatusCode.OK, Accepted);

        _ = await Client(canned).AddTodo(_api, new TodoAdd("call mon", Due: null, Links: null, "c2"), Token);

        Assert.Equal("""{"text":"call mon","clientId":"c2"}""", Compact(canned.Seen[0].Body));
    }

    [Fact]
    public async Task ChangeTodo_sends_the_version_and_only_the_fields_it_changes_an_empty_day_clears()
    {
        var canned = new Canned(HttpStatusCode.OK, Accepted);
        var client = Client(canned);

        _ = await client.ChangeTodo(_api, "td_x", new TodoChange(2, Due: string.Empty), Token);
        _ = await client.ChangeTodo(_api, "td_x", new TodoChange(2, Text: "new", Links: []), Token);

        Assert.Equal(["/sindri/todos/td_x/change", "/sindri/todos/td_x/change"], canned.Seen.Select(s => s.Uri.AbsolutePath), StringComparer.Ordinal);
        Assert.Equal(["""{"version":2,"due":""}""", """{"version":2,"text":"new","links":[]}"""], canned.Seen.Select(s => Compact(s.Body)), StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_four_state_writes_post_to_their_routes_with_the_version_when_there_is_one()
    {
        var canned = new Canned(HttpStatusCode.OK, Accepted);
        var client = Client(canned);

        _ = await client.DoneTodo(_api, "td_x", 3, Token);
        _ = await client.ReopenTodo(_api, "td_x", version: null, Token);
        _ = await client.RemoveTodo(_api, "td_x", 4, Token);
        _ = await client.RestoreTodo(_api, "td_x", version: null, Token);

        Assert.Equal(["/sindri/todos/td_x/done", "/sindri/todos/td_x/reopen", "/sindri/todos/td_x/remove", "/sindri/todos/td_x/restore"], canned.Seen.Select(s => s.Uri.AbsolutePath), StringComparer.Ordinal);
        Assert.All(canned.Seen, s => Assert.Equal(HttpMethod.Post, s.Method));
        Assert.Equal(["""{"version":3}""", "{}", """{"version":4}""", "{}"], canned.Seen.Select(s => Compact(s.Body)), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_refusal_comes_back_as_the_error_with_its_code()
    {
        var problem = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-TODO-409", "Changed somewhere else.");

        var changed = await Client(new Canned(problem.Status, problem.Body)).ChangeTodo(_api, "td_x", new TodoChange(1, Text: "x"), Token);

        Assert.True(changed.IsError);
        Assert.Equal("SND-TODO-409", changed.Error.Code);
    }

    [Fact]
    public async Task An_accepted_write_makes_the_next_list_read_wait_on_its_sequence()
    {
        var canned = new Canned(HttpStatusCode.OK, Accepted);
        var client = Client(canned);

        _ = await client.DoneTodo(_api, "td_x", 3, Token);
        canned.Next = GoldenFiles.Api("todos", "list");
        _ = await client.Todos(_api, "all", Token);

        Assert.Equal("41", canned.Seen[1].After);
    }

    private static string Compact(string json) => JsonNode.Parse(json)!.ToJsonString();

    private static DateOnly? Day(JsonNode? node) => node is null ? null : DateOnly.ParseExact(node.GetValue<string>(), "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DateTimeOffset? Moment(JsonNode? node) => node is null ? null : DateTimeOffset.Parse(node.GetValue<string>(), CultureInfo.InvariantCulture);

    private sealed record Request(HttpMethod Method, Uri Uri, string Body, string? After);

    private sealed class Canned(HttpStatusCode code, string body) : HttpMessageHandler
    {
        public List<Request> Seen { get; } = [];

        public string? Next { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var sent = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Seen.Add(new Request(request.Method, request.RequestUri!, sent, request.Headers.TryGetValues("X-Sindri-After", out var after) ? after.Single() : null)); // why: HttpClient always sets the URI
            return new HttpResponseMessage(code) { Content = new StringContent(Next ?? body, Encoding.UTF8, "application/json") };
        }
    }
}
