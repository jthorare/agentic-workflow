using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The todo routes' goldens (todo spec §4.4, §10 Ph1a): 'GET /sindri/todos?state=all' on a list written through the real routes (a done todo, an edited one with links and a date, one added by a session, one added by a piped
/// CLI call, a removed one and a plain one), and the answer of 'POST /sindri/todos'. The clock stands at the epoch, so every instant is fixed. The owner's Home panel (Ph2) builds against the list file.
/// </summary>
internal static class TodoScenarios
{
    private static readonly string[] _markSlugs = ["todos-done", "todos-reopen", "todos-remove", "todos-restore"];

    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } = [("todos", "list"), ("todos-add", "accepted"), ("todos-change", "clear-due"), ("todos-done", "accepted"), ("todos-reopen", "accepted"), ("todos-remove", "accepted"), ("todos-restore", "accepted"), ("todos-keep", "sindri"), ("todos-keep-undo", "accepted"), ("todos-hold-apply", "accepted"), ("todos-hold-undo", "accepted")];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Starts the scenario's host: the item profile, approved by the body.</summary>
    public static Task<GoldenHost> Start(CancellationToken ct) => GoldenHost.Start(temporary: true, ct, ItemScenarios.ProfileYaml);

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        await ViewScenarios.Approve(host, ct);
        await ViewScenarios.CatchUp(host, ct);
        var human = ItemScenarios.Human(host);
        if (slug == "todos-add")
        {
            return (await host.Http.Ask(Post("/sindri/todos", Request(slug, scenario), human))).Body;
        }

        if (slug == "todos-change")
        {
            var made = await Add(host, human, """{"text":"call the dentist","due":"2026-11-13","clientId":"c-1"}""", string.Empty, ct);
            return (await host.Http.Ask(Post($"/sindri/todos/{made}/change", Request(slug, scenario), human))).Body;
        }

        if (slug.StartsWith("todos-keep", StringComparison.Ordinal) || slug.StartsWith("todos-hold", StringComparison.Ordinal))
        {
            return await Pick(host, human, slug, ct);
        }

        if (_markSlugs.Contains(slug))
        {
            return await Mark(host, human, slug, scenario, ct);
        }

        var cert = await Add(host, human, """{"text":"renew the cert","due":"2025-12-30","clientId":"c-1"}""", string.Empty, ct);
        var offsite = await Add(host, human, """{"text":"reply to Sam","due":"2026-01-02","clientId":"c-2"}""", string.Empty, ct);
        _ = await Add(host, human, """{"text":"rotate the staging key","due":"2025-12-31","links":["acme/api#12","ENG-12"],"clientId":"c-3"}""", string.Empty, ct);
        _ = await Add(host, ItemScenarios.Session("demo"), """{"text":"check the flaky sync test","clientId":"c-4"}""", string.Empty, ct);
        _ = await Add(host, human, """{"text":"answer the review comments on acme/web#40","due":"2026-01-09","clientId":"c-5"}""", "?fence=1", ct);
        var room = await Add(host, human, """{"text":"book the offsite room","clientId":"c-6"}""", string.Empty, ct);
        _ = await host.Http.Ask(Post($"/sindri/todos/{cert}/done", "{}", human));
        await ViewScenarios.CatchUp(host, ct);
        _ = await host.Http.Ask(Post($"/sindri/todos/{offsite}/change", """{"version":1,"text":"reply to Sam about the offsite"}""", human));
        await ViewScenarios.CatchUp(host, ct);
        _ = await host.Http.Ask(Post($"/sindri/todos/{room}/remove", "{}", human));
        await ViewScenarios.CatchUp(host, ct);
        return (await host.Http.Ask(Get("/sindri/todos?state=all", human))).Body;
    }

    // the picks (todo spec D11, REQ-E7): a todo changed in both places, or a hold of the note's changes, written to the ledger the way the sync will; the keep and the hold pick post the request file's bytes, and each Undo follows its pick inside the 10 s
    private static async Task<string> Pick(GoldenHost host, (string Name, string Value)[] human, string slug, CancellationToken ct)
    {
        const string Hash = "sha256:3f1c0a9e5b7d2c4e6a8b0d1f3e5c7a9b2d4f6e8a0c1b3d5f7e9a2c4b6d8f0e1a";
        var made = await Add(host, human, """{"text":"call the dentist","clientId":"c-1"}""", string.Empty, ct);
        var id = Core.Ids.TodoId.From(made);
        if (slug.StartsWith("todos-keep", StringComparison.Ordinal))
        {
            var mine = new TodoSide("call the dentist", Due: null, Links: [], Done: false, Removed: false);
            var theirs = new TodoSide("call the dentist about the crown", new DateOnly(2026, 1, 5), Links: [], Done: false, Removed: false);
            await ViewScenarios.Append(host, EventTypes.TodoConflicted, new TodoConflicted(id, mine, theirs, Hash), $"todo.conflicted:{made}:{Hash}", ct);
            await ViewScenarios.CatchUp(host, ct);
            var kept = await host.Http.Ask(Post($"/sindri/todos/{made}/keep", Request("todos-keep", "sindri"), human));
            if (slug == "todos-keep")
            {
                return kept.Body;
            }

            await ViewScenarios.CatchUp(host, ct);
            return (await host.Http.Ask(Post($"/sindri/todos/{made}/keep/undo", "{}", human))).Body;
        }

        var second = await Add(host, human, """{"text":"reply to Sam","clientId":"c-2"}""", string.Empty, ct);
        var changes = new[] { new TodoHeldChange(id, "removed", NoteLine: null), new TodoHeldChange(Core.Ids.TodoId.From(second), "edited", $"- [ ] reply to Sam about the offsite ^td-{second[3..]}") };
        await ViewScenarios.Append(host, EventTypes.TodoNoteHeld, new TodoNoteHeld(changes, Hash, "service:todo-note"), "todo.note.held:" + Hash, ct);
        await ViewScenarios.CatchUp(host, ct);
        var applied = await host.Http.Ask(Post("/sindri/todos/note/hold/apply", Request("todos-hold-apply", "accepted"), human));
        if (slug == "todos-hold-apply")
        {
            return applied.Body;
        }

        await ViewScenarios.CatchUp(host, ct);
        return (await host.Http.Ask(Post("/sindri/todos/note/hold/undo", "{}", human))).Body;
    }

    // puts a todo into the state the verb needs (reopen: done, restore: removed), then posts the verb with the request file's bytes; a done or remove starts from the open todo at version 1, the others from version 2
    private static async Task<string> Mark(GoldenHost host, (string Name, string Value)[] human, string slug, string scenario, CancellationToken ct)
    {
        var made = await Add(host, human, """{"text":"call the dentist","clientId":"c-1"}""", string.Empty, ct);
        var first = string.Equals(slug, "todos-reopen", StringComparison.Ordinal) ? "done" : string.Equals(slug, "todos-restore", StringComparison.Ordinal) ? "remove" : null;
        if (first is not null)
        {
            _ = await host.Http.Ask(Post($"/sindri/todos/{made}/{first}", "{}", human));
            await ViewScenarios.CatchUp(host, ct);
        }

        var verb = slug["todos-".Length..];
        // why: done and remove take their version from the one shared request file; reopen and restore need version 2 (the state change before them was version 2), so each has its own
        var file = string.Equals(verb, "done", StringComparison.Ordinal) || string.Equals(verb, "remove", StringComparison.Ordinal) ? "todos-done" : slug;
        return (await host.Http.Ask(Post($"/sindri/todos/{made}/{verb}", Request(file, scenario), human))).Body;
    }

    /// <summary>The exact bytes the scenario posts: 'Golden/ApiRequests/&lt;slug&gt;/&lt;scenario&gt;.json', read as they are (no re-serializing), so the file is what the server is proven to read; the UI lanes build their client against the same file.</summary>
    internal static string Request(string slug, string scenario) =>
        File.ReadAllText(Golden.SourcePath(typeof(TodoScenarios).Assembly, $"Golden/ApiRequests/{slug}/{scenario}.json"));

    // why: each write reads the todo views, which wait for the writes before it, so the views catch up after each one
    private static async Task<string> Add(GoldenHost host, (string Name, string Value)[] caller, string json, string query, CancellationToken ct)
    {
        var answer = await host.Http.Ask(Post("/sindri/todos" + query, json, caller));
        await ViewScenarios.CatchUp(host, ct);
        return JsonDocument.Parse(answer.Body).RootElement.GetProperty("id").GetString()!;
    }
}
