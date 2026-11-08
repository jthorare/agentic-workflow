using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Linear;

namespace Aw.Sindri.Ui.Tests.Todo;

/// <summary>The client's todo note calls (todo spec §4.4): the note route read through the hand-written goldens, the conflict part of a todo, the pick routes and bodies, and an older Sindri's bare 404.</summary>
[Lane(TestLane.Unit)]
public sealed class SindriApiClientTodoNoteTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SindriApiClient Client(HttpMessageHandler handler) => new(new HttpClient(handler), new InMemoryHumanToken("tok-1"), TimeProvider.System);

    [Fact]
    public void The_routes_name_the_note_the_keep_pair_and_the_three_hold_calls()
    {
        Assert.Equal(
            ["/sindri/todos/note", "/sindri/todos/td_x/keep", "/sindri/todos/td_x/keep/undo", "/sindri/todos/note/hold/apply", "/sindri/todos/note/hold/restore", "/sindri/todos/note/hold/undo"],
            [TodoRoutes.Note, TodoRoutes.Keep("td_x"), TodoRoutes.KeepUndo("td_x"), TodoRoutes.HoldApply, TodoRoutes.HoldRestore, TodoRoutes.HoldUndo],
            StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_note_golden_reads_the_state_the_conflict_the_pick_and_the_line_that_cannot_sync()
    {
        var handler = new Canned(HttpStatusCode.OK, GoldenFiles.Api("todos-note", "note"));

        var read = await Client(handler).TodoNote(_api, Token);

        Assert.True(read.IsSuccess, read.IsError ? read.Error.Message : string.Empty);
        var note = read.Value;
        Assert.Equal(("syncing", "notes", "Todo.md", null), (note.State, note.Vault, note.Note, note.LastError));
        Assert.Equal(DateTimeOffset.Parse("2026-01-01T09:59:30Z", CultureInfo.InvariantCulture), note.LastCheckAt);
        Assert.Equal(DateTimeOffset.Parse("2026-01-01T09:58:12Z", CultureInfo.InvariantCulture), note.LastSyncAt);
        Assert.Equal(["td_01jc5a8xq0n4w7ms2t9r3vbk6e"], note.Conflicts);
        Assert.Null(note.Hold);
        var pick = Assert.Single(note.Picks);
        Assert.Equal(("conflict", "td_01jc5d4m7p2q9r5s8t1v3w6x0y", "obsidian", "human:ui"), (pick.Kind, pick.Id, pick.Kept, pick.By));
        Assert.Equal(DateTimeOffset.Parse("2026-01-01T10:00:08Z", CultureInfo.InvariantCulture), pick.UndoUntil);
        var line = Assert.Single(note.CantSync);
        Assert.Equal((14, "it has a markdown link"), (line.Line, line.Reason));
        var seen = Assert.Single(handler.Seen);
        Assert.Equal((HttpMethod.Get, "/sindri/todos/note"), (seen.Method, seen.Uri.AbsolutePath));
    }

    [Fact]
    public async Task The_hold_golden_reads_the_hash_the_threshold_base_and_every_held_change()
    {
        var read = await Client(new Canned(HttpStatusCode.OK, GoldenFiles.Api("todos-note", "hold"))).TodoNote(_api, Token);

        Assert.True(read.IsSuccess, read.IsError ? read.Error.Message : string.Empty);
        var hold = read.Value.Hold!;
        Assert.Equal(("sha256:3f1c0a9e5b7d2c4e6a8b0d1f3e5c7a9b2d4f6e8a0c1b3d5f7e9a2c4b6d8f0e1a", 20, DateTimeOffset.Parse("2026-01-01T10:04:41Z", CultureInfo.InvariantCulture)), (hold.NoteHash, hold.OpenInBase, hold.HeldAt));
        Assert.Equal(["removed", "removed", "edited", "reopened"], hold.Changes.Select(c => c.Kind), StringComparer.Ordinal);
        Assert.Equal(("td_01jc5c90a2b7k4m8q1r6t3w9xz", "call the dentist", "- [ ] call the dentist about the crown 📅 2026-01-05 ^td-01jc5c90a2b7k4m8q1r6t3w9xz"), (hold.Changes[2].Id, hold.Changes[2].Text, hold.Changes[2].NoteLine));
        Assert.Null(hold.Changes[0].NoteLine);
        Assert.Empty(read.Value.Conflicts);
        Assert.Empty(read.Value.Picks);
    }

    [Fact]
    public async Task The_conflict_golden_reads_as_the_conflict_part_of_a_todo_row()
    {
        var todo = GoldenFiles.Node("todos-note", "conflict")["todo"]!;
        var list = new JsonObject
        {
            ["asOf"] = GoldenFiles.Node("todos-note", "conflict")["asOf"]!.DeepClone(),
            ["state"] = "all",
            ["counts"] = new JsonObject { ["open"] = 1, ["done"] = 0, ["removed"] = 0 },
            ["todos"] = new JsonArray(todo.DeepClone()),
        };

        var read = await Client(new Canned(HttpStatusCode.OK, list.ToJsonString())).Todos(_api, "all", Token);

        Assert.True(read.IsSuccess, read.IsError ? read.Error.Message : string.Empty);
        var conflict = read.Value.Todos[0].Conflict!;
        Assert.Equal("sha256:9b2d4f6e8a0c1b3d5f7e9a2c4b6d8f0e1a3f1c0a9e5b7d2c4e6a8b0d1f3e5c7a", conflict.NoteHash);
        Assert.Equal(("reply to Sam about the offsite", new DateOnly(2026, 1, 2), true, false), (conflict.Sindri.Text, conflict.Sindri.Due, conflict.Sindri.Done, conflict.Sindri.Removed));
        Assert.Equal(("reply to Sam about the offsite venue", new DateOnly(2026, 1, 3), false), (conflict.Obsidian.Text, conflict.Obsidian.Due, conflict.Obsidian.Done));
        Assert.Equal(["sindri://linear/ENG-12"], conflict.Obsidian.Links);
        Assert.Empty(conflict.Sindri.Links!);
    }

    [Fact]
    public async Task A_bare_404_is_an_older_Sindri_without_the_note_route()
    {
        var read = await Client(new Canned(HttpStatusCode.NotFound, string.Empty)).TodoNote(_api, Token);

        Assert.True(read.IsError);
        Assert.True(OlderSindri.IsNotServed(read.Error));
    }

    [Fact]
    public async Task Keep_posts_the_side_to_the_todos_keep_route_and_reads_the_accepted_pick()
    {
        var todo = GoldenFiles.Node("todos-note", "conflict")["todo"]!.ToJsonString();
        var handler = new Canned(HttpStatusCode.OK, $$"""{"todo":{{todo}},"undoUntil":"2026-01-01T10:00:10.0000000Z"}""");

        var kept = await Client(handler).KeepTodo(_api, "td_x", "obsidian", Token);

        Assert.True(kept.IsSuccess, kept.IsError ? kept.Error.Message : string.Empty);
        Assert.Equal(("td_01jc5a8xq0n4w7ms2t9r3vbk6e", DateTimeOffset.Parse("2026-01-01T10:00:10Z", CultureInfo.InvariantCulture)), (kept.Value.Todo!.Id, kept.Value.UndoUntil));
        var seen = Assert.Single(handler.Seen);
        Assert.Equal((HttpMethod.Post, "/sindri/todos/td_x/keep", """{"side":"obsidian"}"""), (seen.Method, seen.Uri.AbsolutePath, Compact(seen.Body)));
    }

    [Fact]
    public async Task The_undo_calls_post_with_no_body_and_the_hold_picks_send_the_note_hash()
    {
        var handler = new Canned(HttpStatusCode.OK, """{"kept":null}""");
        var client = Client(handler);

        _ = await client.UndoKeepTodo(_api, "td_x", Token);
        _ = await client.UndoHold(_api, Token);
        _ = await client.RestoreHold(_api, "sha256:ab", Token);
        _ = await client.ApplyHold(_api, "sha256:cd", Token);

        Assert.Equal(["/sindri/todos/td_x/keep/undo", "/sindri/todos/note/hold/undo", "/sindri/todos/note/hold/restore", "/sindri/todos/note/hold/apply"], handler.Seen.Select(s => s.Uri.AbsolutePath), StringComparer.Ordinal);
        Assert.All(handler.Seen, s => Assert.Equal(HttpMethod.Post, s.Method));
        Assert.Equal([string.Empty, string.Empty, """{"noteHash":"sha256:ab"}""", """{"noteHash":"sha256:cd"}"""], handler.Seen.Select(s => s.Body.Length == 0 || s.Body == "{}" ? string.Empty : Compact(s.Body)), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_pick_refusal_comes_back_with_its_code()
    {
        var problem = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-TODO-409", "The note changed again: now 9.");

        var picked = await Client(new Canned(problem.Status, problem.Body)).ApplyHold(_api, "sha256:ab", Token);

        Assert.True(picked.IsError);
        Assert.Equal("SND-TODO-409", picked.Error.Code);
    }

    private static string Compact(string json) => JsonNode.Parse(json)!.ToJsonString();

    private sealed record Request(HttpMethod Method, Uri Uri, string Body);

    private sealed class Canned(HttpStatusCode code, string body) : HttpMessageHandler
    {
        public List<Request> Seen { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var sent = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Seen.Add(new Request(request.Method, request.RequestUri!, sent)); // why: HttpClient always sets the URI
            return new HttpResponseMessage(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
