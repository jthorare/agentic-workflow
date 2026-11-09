using System.Net;
using System.Text.Json;
using Aw.Sindri.Tests.ApiGoldens;
using Aw.Sindri.Tests.Callers;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.Todos;

/// <summary>'POST /sindri/todos/note/set' and '/stop' over a real control plane (todo spec §4.4, §9 TP-4, TP-13): the same validated profile edit as the CLI, the answer's hash, the ifHash guard, and a session caller.</summary>
public sealed class TodoNoteWriteRouteTests
{
    private const string Set = "/sindri/todos/note/set";
    private const string Stop = "/sindri/todos/note/stop";
    private const string Vaults = "context:\n  sources:\n    library:\n      vaults:\n        - { name: notes, path: /work/notes }\n";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<GoldenHost> Start()
    {
        var host = await GoldenHost.Start(temporary: true, Token, ItemScenarios.ProfileYaml + Vaults);
        await ViewScenarios.Approve(host, Token);
        return host;
    }

    private static string ProfilePath(GoldenHost host) => Path.Combine(host.Home, "profile", "profile.yaml");

    private static JsonElement Root(Answer answer) => JsonDocument.Parse(answer.Body).RootElement.Clone();

    [Fact, Trait("Lane", "integration")]
    public async Task Set_writes_the_key_answers_changed_and_the_new_profile_hash_and_a_repeat_is_unchanged()
    {
        await using var host = await Start();
        var human = ItemScenarios.Human(host);

        var first = await host.Http.Ask(Post(Set, """{"vault":"notes","note":"Todo.md"}""", human));

        Assert.Equal(HttpStatusCode.OK, first.Status);
        Assert.True(Root(first).GetProperty("changed").GetBoolean());
        var hash = Root(first).GetProperty("profileHash").GetString();
        Assert.Equal((await host.LiveHash(Token)).Value, hash);
        Assert.Contains("vault: \"notes\"", await File.ReadAllTextAsync(ProfilePath(host), Token), StringComparison.Ordinal);

        var again = await host.Http.Ask(Post(Set, $$"""{"vault":"notes","note":"Todo.md","ifHash":"{{hash}}"}""", human));

        Assert.Equal(HttpStatusCode.OK, again.Status);
        Assert.False(Root(again).GetProperty("changed").GetBoolean());
        Assert.Equal(hash, Root(again).GetProperty("profileHash").GetString());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_ifHash_that_is_not_the_live_hash_is_409_SND_TODO_409_and_nothing_is_written()
    {
        await using var host = await Start();
        var before = await File.ReadAllTextAsync(ProfilePath(host), Token);

        var answer = await host.Http.Ask(Post(Set, $$"""{"vault":"notes","note":"Todo.md","ifHash":"{{new string('0', 64)}}"}""", ItemScenarios.Human(host)));

        Assert.Equal(HttpStatusCode.Conflict, answer.Status);
        Assert.Contains("SND-TODO-409", answer.Body, StringComparison.Ordinal);
        Assert.Equal(before, await File.ReadAllTextAsync(ProfilePath(host), Token));
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("""{"vault":"elsewhere","note":"Todo.md"}""", "todo.obsidian.vault")]
    [InlineData("""{"vault":"notes","note":"../Todo.md"}""", "todo.obsidian.note")]
    [InlineData("""{"note":"Todo.md"}""", "todo.obsidian.vault")]
    public async Task A_bad_vault_or_note_is_400_SND_TODO_002_with_the_key_path_and_nothing_is_written(string body, string key)
    {
        await using var host = await Start();
        var before = await File.ReadAllTextAsync(ProfilePath(host), Token);

        var answer = await host.Http.Ask(Post(Set, body, ItemScenarios.Human(host)));

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Contains("SND-TODO-002", answer.Body, StringComparison.Ordinal);
        Assert.Contains(key, answer.Body, StringComparison.Ordinal);
        Assert.Equal(before, await File.ReadAllTextAsync(ProfilePath(host), Token));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Stop_removes_the_key_and_a_second_stop_is_unchanged()
    {
        await using var host = await Start();
        var human = ItemScenarios.Human(host);
        var before = await File.ReadAllTextAsync(ProfilePath(host), Token);
        _ = await host.Http.Ask(Post(Set, """{"vault":"notes","note":"Todo.md"}""", human));

        var stopped = await host.Http.Ask(Post(Stop, "{}", human));
        var again = await host.Http.Ask(Post(Stop, "{}", human));

        Assert.True(Root(stopped).GetProperty("changed").GetBoolean());
        Assert.False(Root(again).GetProperty("changed").GetBoolean());
        Assert.Equal(before, await File.ReadAllTextAsync(ProfilePath(host), Token));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_session_caller_with_no_launch_can_set_and_stop()
    {
        await using var host = await Start();
        var session = ItemScenarios.Session("demo");

        Assert.Equal(HttpStatusCode.OK, (await host.Http.Ask(Post(Set, """{"vault":"notes","note":"Todo.md"}""", session))).Status);
        Assert.Equal(HttpStatusCode.OK, (await host.Http.Ask(Post(Stop, "{}", session))).Status);
    }
}
