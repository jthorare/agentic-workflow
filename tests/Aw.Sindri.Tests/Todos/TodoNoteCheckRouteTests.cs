using System.Net;
using System.Text.Json;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Todos.Note;
using Aw.Sindri.Tests.ApiGoldens;
using Aw.Sindri.Tests.Callers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.Todos;

/// <summary>'POST /sindri/todos/note/check' over a real control plane on a temp vault (todo spec §4.4, §9 TP-4, TP-13): the counts, a note that doesn't exist, the key's refusals, a symlink, the swap probe through its fake, a session caller, and that nothing is written.</summary>
public sealed class TodoNoteCheckRouteTests : IDisposable
{
    private const string Route = "/sindri/todos/note/check";

    private static readonly JsonSerializerOptions _web = new(JsonSerializerDefaults.Web);

    private readonly string _vault = Directory.CreateTempSubdirectory("aw-todo-vault-").FullName;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(_vault, recursive: true);

    private string Profile => ItemScenarios.ProfileYaml + $"context:\n  sources:\n    library:\n      vaults:\n        - {{ name: notes, path: {_vault}, deny: [\"private/**\"] }}\n";

    private async Task<GoldenHost> Start(ITodoNoteDisk? disk = null)
    {
        var host = await GoldenHost.Start(temporary: true, Token, Profile, extra: disk is null ? null : services =>
        {
            _ = services.RemoveAll<ITodoNoteDisk>();
            _ = services.AddSingleton(disk);
        });
        await ViewScenarios.Approve(host, Token);
        await ViewScenarios.CatchUp(host, Token);
        return host;
    }

    private static string Body(string vault, string note) => JsonSerializer.Serialize(new { vault, note }, _web);

    private static JsonElement Root(Answer answer) => JsonDocument.Parse(answer.Body).RootElement.Clone();

    private static async Task<int> LedgerHead(GoldenHost host) =>
        (await host.App.Services.GetRequiredService<ILedgerStore>().ReadAfter(0, 10_000, Token)).Value.Count;

    [Fact, Trait("Lane", "integration")]
    public async Task The_counts_of_an_existing_note_come_back_and_nothing_is_written_to_the_note_or_the_ledger()
    {
        const string Note = "# Todo\n- [ ] reply to Sam\n- [ ] read [docs](https://example.com)\n- [x] pay rent\n\nnotes\n";
        await File.WriteAllTextAsync(Path.Combine(_vault, "Todo.md"), Note, Token);
        await using var host = await Start();
        var before = await LedgerHead(host);

        var answer = await host.Http.Ask(Post(Route, Body("notes", "Todo.md"), ItemScenarios.Human(host)));

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        var json = Root(answer);
        Assert.Equal((true, 2, 1, 3, 1, 1, 0, 1, false), (json.GetProperty("exists").GetBoolean(), json.GetProperty("open").GetInt32(), json.GetProperty("done").GetInt32(), json.GetProperty("other").GetInt32(), json.GetProperty("importable").GetInt32(), json.GetProperty("cantImport").GetInt32(), json.GetProperty("sindriOpen").GetInt32(), json.GetProperty("willImport").GetInt32(), json.GetProperty("overCap").GetBoolean()));
        Assert.Equal([3], json.GetProperty("cantImportLines").EnumerateArray().Select(static l => l.GetInt32()));
        Assert.Equal(Note, await File.ReadAllTextAsync(Path.Combine(_vault, "Todo.md"), Token));
        Assert.Equal(["Todo.md"], Directory.GetFileSystemEntries(_vault).Select(static p => Path.GetFileName(p)), StringComparer.Ordinal);   // the swap probe's two temporary files are gone
        Assert.Equal(before, await LedgerHead(host));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_note_that_does_not_exist_yet_answers_exists_false_with_zero_counts()
    {
        await using var host = await Start();

        var answer = await host.Http.Ask(Post(Route, Body("notes", "Daily/Todo.md"), ItemScenarios.Human(host)));

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        var json = Root(answer);
        Assert.Equal((false, 0, 0), (json.GetProperty("exists").GetBoolean(), json.GetProperty("open").GetInt32(), json.GetProperty("importable").GetInt32()));
        Assert.False(Directory.Exists(Path.Combine(_vault, "Daily")));   // checking creates no folder
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_open_todos_Sindri_already_has_count_against_the_500_limit()
    {
        await File.WriteAllTextAsync(Path.Combine(_vault, "Todo.md"), "- [ ] one\n- [ ] two\n- [ ] three\n", Token);
        await using var host = await Start();
        foreach (var text in new[] { "call the dentist", "book the room" })
        {
            _ = await host.Http.Ask(Post("/sindri/todos", JsonSerializer.Serialize(new { text, clientId = text.Replace(' ', '-') }, _web), ItemScenarios.Human(host)));
            await ViewScenarios.CatchUp(host, Token);
        }

        var json = Root(await host.Http.Ask(Post(Route, Body("notes", "Todo.md"), ItemScenarios.Human(host))));

        Assert.Equal((2, 3, 3), (json.GetProperty("sindriOpen").GetInt32(), json.GetProperty("importable").GetInt32(), json.GetProperty("willImport").GetInt32()));
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("elsewhere", "Todo.md", "todo.obsidian.vault")]
    [InlineData("notes", "../Todo.md", "todo.obsidian.note")]
    [InlineData("notes", "Todo.txt", "todo.obsidian.note")]
    [InlineData("notes", "private/Todo.md", "todo.obsidian.note")]
    public async Task A_bad_vault_or_note_is_400_SND_TODO_002_with_the_key_path(string vault, string note, string key)
    {
        await using var host = await Start();

        var answer = await host.Http.Ask(Post(Route, Body(vault, note), ItemScenarios.Human(host)));

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Contains("SND-TODO-002", answer.Body, StringComparison.Ordinal);
        Assert.Contains(key, answer.Body, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_note_that_is_a_symlink_is_400_SND_TODO_002()
    {
        var real = Path.Combine(_vault, "real.md");
        await File.WriteAllTextAsync(real, "- [ ] one\n", Token);
        _ = File.CreateSymbolicLink(Path.Combine(_vault, "Todo.md"), real);
        await using var host = await Start();

        var answer = await host.Http.Ask(Post(Route, Body("notes", "Todo.md"), ItemScenarios.Human(host)));

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Contains("SND-TODO-002", answer.Body, StringComparison.Ordinal);
        Assert.Contains("symlink", answer.Body, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_disk_without_the_swap_call_answers_canSwap_false_and_is_not_an_error()
    {
        await using var host = await Start(new FakeDisk(new NoteFolderFacts(Writable: true, CanSwap: false)));

        var answer = await host.Http.Ask(Post(Route, Body("notes", "Todo.md"), ItemScenarios.Human(host)));

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.False(Root(answer).GetProperty("canSwap").GetBoolean());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_folder_Sindri_cannot_write_is_400_SND_TODO_002()
    {
        await using var host = await Start(new FakeDisk(new NoteFolderFacts(Writable: false, CanSwap: false)));

        var answer = await host.Http.Ask(Post(Route, Body("notes", "Todo.md"), ItemScenarios.Human(host)));

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Contains("SND-TODO-002", answer.Body, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_session_caller_with_no_launch_can_check_a_note()
    {
        await using var host = await Start();

        var answer = await host.Http.Ask(Post(Route, Body("notes", "Todo.md"), ItemScenarios.Session("demo")));

        Assert.Equal(HttpStatusCode.OK, answer.Status);
    }

    private sealed class FakeDisk(NoteFolderFacts folder) : ITodoNoteDisk
    {
        public NoteFileFacts Inspect(string vaultRoot, string note) => new(Exists: false, IsSymlink: false, LinkCount: 0, Text: null, Unreadable: null);

        public NoteFolderFacts Probe(string vaultRoot, string note) => folder;
    }
}
