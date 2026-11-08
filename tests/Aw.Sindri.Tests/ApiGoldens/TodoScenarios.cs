using System.Text.Json;
using System.Text.RegularExpressions;
using Aw.Kernel.Testing;
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
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } = [("todos", "list"), ("todos-id", "show"), ("todos-add", "accepted"), ("todos-change", "clear-due"), ("todos-done", "accepted"), ("todos-reopen", "accepted"), ("todos-remove", "accepted"), ("todos-restore", "accepted"), ("todos-note-check", "counts"), ("todos-note-set", "changed"), ("todos-note-set", "unchanged"), ("todos-note-set", "stale"), ("todos-note-stop", "changed"), ("todos-note-stop", "unchanged")];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Starts the scenario's host: the item profile, approved by the body.</summary>
    /// <param name="slug">The route; the note check's host also maps a library vault holding the sample note.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The started host.</returns>
    public static Task<GoldenHost> Start(string slug, CancellationToken ct) =>
        GoldenHost.Start(temporary: true, ct, slug switch
        {
            "todos-note-check" => NoteVaultProfile(),
            "todos-note-set" or "todos-note-stop" => ItemScenarios.ProfileYaml + "context:\n  sources:\n    library:\n      vaults:\n        - { name: notes, path: /work/notes }\n",
            _ => ItemScenarios.ProfileYaml,
        });

    // a vault folder at a fixed place under the temp folder with the note the golden counts (3 open, one with a pasted link, 1 done); the answer carries no path
    private static string NoteVaultProfile()
    {
        var vault = Path.Combine(Path.GetTempPath(), "aw-golden-todo-vault");
        _ = Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "Todo.md"), "# Todo\n- [ ] reply to Sam\n- [ ] rotate the staging key\n- [ ] read [the docs](https://example.com)\n- [x] pay rent\n\nnotes\n");
        return ItemScenarios.ProfileYaml + $"context:\n  sources:\n    library:\n      vaults:\n        - {{ name: notes, path: {vault} }}\n";
    }

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        await ViewScenarios.Approve(host, ct);
        await ViewScenarios.CatchUp(host, ct);
        var human = ItemScenarios.Human(host);
        if (slug.StartsWith("todos-note-", StringComparison.Ordinal))
        {
            return string.Equals(slug, "todos-note-check", StringComparison.Ordinal)
                ? (await host.Http.Ask(Post("/sindri/todos/note/check", Request(slug, scenario), human))).Body
                : await NoteWrite(host, slug, scenario, human);
        }

        if (slug == "todos-add")
        {
            return (await host.Http.Ask(Post("/sindri/todos", Request(slug, scenario), human))).Body;
        }

        if (slug == "todos-change")
        {
            var made = await Add(host, human, """{"text":"call the dentist","due":"2026-11-13","clientId":"c-1"}""", string.Empty, ct);
            return (await host.Http.Ask(Post($"/sindri/todos/{made}/change", Request(slug, scenario), human))).Body;
        }

        if (_markSlugs.Contains(slug))
        {
            return await Mark(host, human, slug, scenario, ct);
        }

        if (slug == "todos-id")
        {
            return await Show(host, human, ct);
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

    // GET /sindri/todos/{id} for a todo with two links and an open both-places conflict (#2355): the answer must be the list's TodoItem shape, links described, no internal fields
    private static async Task<string> Show(GoldenHost host, (string Name, string Value)[] human, CancellationToken ct)
    {
        var made = await Add(host, human, """{"text":"rotate the staging key","due":"2025-12-31","links":["acme/api#12","ENG-12"],"clientId":"c-1"}""", string.Empty, ct);
        var sindri = new Core.Events.TodoSide("rotate the staging key", new DateOnly(2025, 12, 31), ["acme/api#12", "ENG-12"], Done: false, Removed: false);
        var obsidian = sindri with { Text = "rotate the prod key" };
        await ViewScenarios.Append(host, Core.Events.EventTypes.TodoConflicted, new Core.Events.TodoConflicted(Core.Ids.TodoId.From(made), sindri, obsidian, NoteHash: "ab12"), "todo.conflicted:golden", ct);
        await ViewScenarios.CatchUp(host, ct);
        return (await host.Http.Ask(Get($"/sindri/todos/{made}", human))).Body;
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

    // set: "changed" is the first write, "unchanged" repeats it, "stale" sends another hash; stop: "changed" follows a set, "unchanged" is a stop with no key. The profile hash follows the machine's profile text, so the golden pins its shape.
    private static async Task<string> NoteWrite(GoldenHost host, string slug, string scenario, (string Name, string Value)[] human)
    {
        const string Set = "/sindri/todos/note/set";
        var isSet = string.Equals(slug, "todos-note-set", StringComparison.Ordinal);
        if (string.Equals(scenario, "unchanged", StringComparison.Ordinal) && isSet)
        {
            _ = await host.Http.Ask(Post(Set, Request("todos-note-set", "changed"), human));
        }
        else if (!isSet && string.Equals(scenario, "changed", StringComparison.Ordinal))
        {
            _ = await host.Http.Ask(Post(Set, Request("todos-note-set", "changed"), human));
        }

        var route = isSet ? Set : "/sindri/todos/note/stop";
        var written = (await host.Http.Ask(Post(route, Request(slug, scenario), human))).Body;
        return Regex.Replace(written, "(?<=\"profileHash\"\\s*:\\s*\")[0-9a-f]{64}", new string('0', 64), RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
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
