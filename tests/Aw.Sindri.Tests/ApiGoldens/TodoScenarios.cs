using System.Text.Json;
using Aw.Sindri.Testing;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The todo routes' goldens (todo spec §4.4, §10 Ph1a): 'GET /sindri/todos?state=all' on a list written through the real routes (a done todo, an edited one with links and a date, one added by a session, one added by a piped
/// CLI call, a removed one and a plain one), and the answer of 'POST /sindri/todos'. The clock stands at the epoch, so every instant is fixed. The owner's Home panel (Ph2) builds against the list file.
/// </summary>
internal static class TodoScenarios
{
    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } = [("todos", "list"), ("todos-add", "accepted")];

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
            return (await host.Http.Ask(Post("/sindri/todos", """{"text":"rotate the staging key","due":"2026-11-13","links":["ENG-12"],"clientId":"c-1"}""", human))).Body;
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

    // why: each write reads the todo views, which wait for the writes before it, so the views catch up after each one
    private static async Task<string> Add(GoldenHost host, (string Name, string Value)[] caller, string json, string query, CancellationToken ct)
    {
        var answer = await host.Http.Ask(Post("/sindri/todos" + query, json, caller));
        await ViewScenarios.CatchUp(host, ct);
        return JsonDocument.Parse(answer.Body).RootElement.GetProperty("id").GetString()!;
    }
}
