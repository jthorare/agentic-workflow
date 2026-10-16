using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Host;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// API P3's golden scenarios (plan §6, UI #197's names included): the queue, the item list, item detail and timeline, and
/// the two item commands. Every scenario starts its own host on <see cref="ProfileYaml"/> (two repos), approves it, makes
/// its items through the API as the human, drives Projector.CatchUp on the fake clock before it reads, and
/// stalls the views with an 'item.created' whose payload doesn't parse (UI spec §18.2). Ids are sequential, so every
/// 'ITM-…' is stable. 'needs-decision' and 'running' scenarios belong to P4 and P11 (they need engine states).
/// </summary>
internal static class ItemScenarios
{
    /// <summary>The profile the item scenarios run on: two repos, so a session can sit outside an item's repo.</summary>
    public const string ProfileYaml = "schemaVersion: 1\nuser: golden\nrepos:\n  - { name: demo, path: /work/demo }\n  - { name: other, path: /work/other }\nservices:\n  heartbeat: { every: 5s }\n";

    private const string NoSuchItem = "ITM-000000000000000000000000000000ff";

    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("queue", "fixture"), ("queue", "empty"), ("queue", "stalled"), ("queue", "large"), ("queue", "session-scoped"), ("queue", "rebuilding"),
        ("items", "list-empty"), ("items", "list-open"), ("items", "list-state-done"), ("items", "list-repo"), ("items", "list-session-other-repo"), ("items", "list-bad-state"),
        ("items", "created"), ("items", "created-repo"), ("items", "created-link"), ("items", "invalid-title"), ("items", "unknown-repo"), ("items", "refused-session"), ("items", "refused-origin"),
        ("items-id", "new"), ("items-id", "needs-decision"), ("items-id", "after-decide"), ("items-id", "after-close"), ("items-id", "not-found"), ("items-id", "cancelled"), ("items-id", "alias"), ("items-id", "with-body"), ("items-id", "session-other-repo"), ("items-id", "bad-id"),
        ("items-id-timeline", "fixture"), ("items-id-timeline", "page-2"), ("items-id-timeline", "closed"), ("items-id-timeline", "not-found"),
        ("items-id-close", "accepted"), ("items-id-close", "stale-version"), ("items-id-close", "accepted-cancelled"), ("items-id-close", "already-closed"), ("items-id-close", "not-found"),
        ("items-id-close", "refused-session"), ("items-id-close", "while-stalled"), ("items-id-close", "while-rebuilding"),
    ];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Runs the scenario against <paramref name="host"/> (started on <see cref="ProfileYaml"/>) and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        await ViewScenarios.Approve(host, ct);
        return slug switch
        {
            "queue" => await QueueBody(host, scenario, ct),
            "items" when scenario.StartsWith("list-", StringComparison.Ordinal) => await ListBody(host, scenario, ct),
            "items" => await CreateBody(host, scenario),
            "items-id" => await DetailBody(host, scenario, ct),
            "items-id-timeline" => await TimelineBody(host, scenario, ct),
            _ => await CloseBody(host, scenario, ct),
        };
    }

    private static async Task<string> QueueBody(GoldenHost host, string scenario, CancellationToken ct)
    {
        switch (scenario)
        {
            case "fixture":
                var made = await CreateMany(host, 4, repo: "demo");
                _ = await RequestCalls.Filed(host, "Split the work.", Session("demo"), ct, item: made[1]);   // item 2 waits on a decision (UI #197's needs-decision row)
                break;
            case "large":
                _ = await CreateMany(host, 30, repo: "demo");
                break;
            case "stalled":
                _ = await CreateMany(host, 2, repo: "demo");
                await Stall(host, ct);
                break;
            case "rebuilding":
                _ = await CreateMany(host, 2, repo: "demo");
                await ViewScenarios.CatchUp(host, ct);
                host.App.Services.GetRequiredService<IProjectorControl>().RequestRebuild();
                return await Send(host, Get("/sindri/queue", Human(host)));
            case "session-scoped":
                _ = await Create(host, "In demo", "demo");
                _ = await Create(host, "In other", "other");
                await ViewScenarios.CatchUp(host, ct);
                return await Send(host, Get("/sindri/queue", Session("demo")));
            default:
                break;   // 'empty'
        }

        await ViewScenarios.CatchUp(host, ct);
        return await Send(host, Get("/sindri/queue", Human(host)));
    }

    private static async Task<string> ListBody(GoldenHost host, string scenario, CancellationToken ct)
    {
        switch (scenario)
        {
            case "list-open":
                _ = await CreateMany(host, 3, repo: "demo");
                break;
            case "list-state-done":
                var id = await Create(host, "Finished", "demo");
                _ = await Create(host, "Still open", "demo");
                await ViewScenarios.CatchUp(host, ct);
                _ = await Close(host, id, "done", 1);
                break;
            case "list-repo":
                _ = await Create(host, "In demo", "demo");
                _ = await Create(host, "In other", "other");
                break;
            case "list-session-other-repo":
                _ = await Create(host, "In other", "other");
                await ViewScenarios.CatchUp(host, ct);
                return await Send(host, Get("/sindri/items", Session("demo")));
            default:
                break;   // 'list-empty', 'list-bad-state'
        }

        await ViewScenarios.CatchUp(host, ct);
        var query = scenario switch
        {
            "list-state-done" => "?state=done",
            "list-repo" => "?repo=demo",
            "list-bad-state" => "?state=sleeping",
            _ => string.Empty,
        };
        return await Send(host, Get("/sindri/items" + query, Human(host)));
    }

    private static async Task<string> CreateBody(GoldenHost host, string scenario)
    {
        var human = Human(host);
        return scenario switch
        {
            "created" => await Send(host, Post("/sindri/items", """{"title":"Try the queue"}""", human)),
            "created-repo" => await Send(host, Post("/sindri/items", """{"title":"Try the queue","repo":"demo"}""", human)),
            "created-link" => await Send(host, Post("/sindri/items", """{"title":"Fix the login redirect","repo":"demo","link":"linear:issue/abc-123"}""", human)),
            "invalid-title" => await Send(host, Post("/sindri/items", """{"title":"   "}""", human)),
            "unknown-repo" => await Send(host, Post("/sindri/items", """{"title":"Try the queue","repo":"nowhere"}""", human)),
            "refused-session" => await Send(host, Post("/sindri/items", """{"title":"Try the queue"}""", Session("demo"))),
            "refused-origin" => await Send(host, Post("/sindri/items", """{"title":"Try the queue"}""", [.. human, ("Origin", "http://localhost:3000")])),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "no such items scenario"),
        };
    }

    private static async Task<string> DetailBody(GoldenHost host, string scenario, CancellationToken ct)
    {
        if (scenario == "not-found")
        {
            return await Send(host, Get("/sindri/items/" + NoSuchItem, Human(host)));
        }

        if (scenario == "bad-id")
        {
            return await Send(host, Get("/sindri/items/not-an-item", Human(host)));
        }

        var id = await CreateForDetail(host, scenario);
        await ViewScenarios.CatchUp(host, ct);
        switch (scenario)
        {
            case "after-close":
                _ = await Close(host, id, "done", 1);
                break;
            case "cancelled":
                _ = await Close(host, id, "cancelled", 1);
                break;
            case "needs-decision" or "after-decide":
                await WaitOnDecision(host, id, decide: scenario == "after-decide", ct);
                break;
            case "alias":
                await AppendFor(host, id, EventTypes.ItemLinked, new ItemLinked(Command(0xb1), ItemId.From(id), RecordKey.From("linear:issue/abc-123"), ItemAlias.From("ENG-142")), "item.linked:golden:alias", ct);
                id = "ENG-142";
                break;
            default:
                break;   // 'new', 'with-body', 'session-other-repo'
        }

        await ViewScenarios.CatchUp(host, ct);
        return await Send(host, Get("/sindri/items/" + id, scenario == "session-other-repo" ? Session("demo") : Human(host)));
    }

    private static Task<string> CreateForDetail(GoldenHost host, string scenario) =>
        scenario == "with-body"
            ? Create(host, "Fix the login redirect", "demo", body: "Users land on /home after a deep link.", link: "linear:issue/abc-123")
            : Create(host, "Try the queue", scenario == "session-other-repo" ? "other" : "demo");

    private static async Task WaitOnDecision(GoldenHost host, string item, bool decide, CancellationToken ct)
    {
        var (_, decision) = await RequestCalls.Filed(host, "Split the work.", Session("demo"), ct, item: item);
        if (decide)
        {
            _ = await RequestCalls.Decide(host, decision, "decline", note: null, Human(host), ct);
        }
    }

    private static async Task<string> TimelineBody(GoldenHost host, string scenario, CancellationToken ct)
    {
        if (scenario == "not-found")
        {
            return await Send(host, Get($"/sindri/items/{NoSuchItem}/timeline", Human(host)));
        }

        var id = await Create(host, "Try the queue", "demo");
        if (scenario == "closed")
        {
            await ViewScenarios.CatchUp(host, ct);
            _ = await Close(host, id, "done", 1);
        }
        else
        {
            for (var n = 1; n <= 100; n++)   // the creation plus 100 more makes 101: a full page and a next
            {
                await AppendFor(host, id, EventTypes.ItemLinked, new ItemLinked(Command(0xc0 + n), ItemId.From(id), RecordKey.From(string.Create(CultureInfo.InvariantCulture, $"linear:issue/abc-{n}")), Alias: null), string.Create(CultureInfo.InvariantCulture, $"item.linked:golden:{n}"), ct);
            }
        }

        await ViewScenarios.CatchUp(host, ct);
        var first = await Send(host, Get($"/sindri/items/{id}/timeline?after=0", Human(host)));
        if (scenario != "page-2")
        {
            return first;
        }

        var next = JsonDocument.Parse(first).RootElement.GetProperty("next").GetInt64();
        return await Send(host, Get(string.Create(CultureInfo.InvariantCulture, $"/sindri/items/{id}/timeline?after={next}"), Human(host)));
    }

    private static async Task<string> CloseBody(GoldenHost host, string scenario, CancellationToken ct)
    {
        if (scenario == "not-found")
        {
            await ViewScenarios.CatchUp(host, ct);   // a close reads the views, which wait for a projector that this host doesn't run
            return await Close(host, NoSuchItem, "done", 1);
        }

        var id = await Create(host, "Try the queue", "demo");
        await ViewScenarios.CatchUp(host, ct);
        switch (scenario)
        {
            case "stale-version":
                return await Close(host, id, "done", 7);
            case "accepted-cancelled":
                return await Close(host, id, "cancelled", 1);
            case "already-closed":
                _ = await Close(host, id, "done", 1);
                await ViewScenarios.CatchUp(host, ct);
                return await Close(host, id, "done", 2);
            case "refused-session":
                return await Send(host, Post($"/sindri/items/{id}/close", """{"reason":"done","itemVersion":1}""", Session("demo")));
            case "while-stalled":
                await Stall(host, ct);
                return await Close(host, id, "done", 1);
            case "while-rebuilding":
                host.App.Services.GetRequiredService<IProjectorControl>().RequestRebuild();
                return await Close(host, id, "done", 1);
            default:
                return await Close(host, id, "done", 1);   // 'accepted'
        }
    }

    internal static (string Name, string Value)[] Human(GoldenHost host) => Callers.CallerRequests.Human(host.Token.Value);

    internal static (string Name, string Value)[] Session(string repo) => [(SindriHeaders.Caller, "session"), (SindriHeaders.Client, "cli"), (SindriHeaders.Repo, repo)];

    private static CommandId Command(int n) => CommandId.From(new Guid(n, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));

    private static async Task<string> Send(GoldenHost host, HttpRequestMessage request) => (await host.Http.Ask(request)).Body;

    private static async Task<string> Close(GoldenHost host, string item, string reason, long version) =>
        await Send(host, Post($"/sindri/items/{item}/close", string.Create(CultureInfo.InvariantCulture, $$"""{"reason":"{{reason}}","itemVersion":{{version}}}"""), Human(host)));

    /// <summary>Creates an item as the human through the API and returns its id.</summary>
    internal static async Task<string> Create(GoldenHost host, string title, string? repo, string? body = null, string? link = null)
    {
        var json = new JsonObject { ["title"] = title, ["repo"] = repo, ["body"] = body, ["link"] = link };
        var answer = await host.Http.Ask(Post("/sindri/items", json.ToJsonString(), Human(host)));
        return JsonDocument.Parse(answer.Body).RootElement.GetProperty("itemId").GetString() ?? throw new InvalidOperationException($"no itemId in {answer.Body}");
    }

    private static async Task<List<string>> CreateMany(GoldenHost host, int count, string repo)
    {
        var ids = new List<string>(count);
        for (var n = 1; n <= count; n++)
        {
            ids.Add(await Create(host, string.Create(CultureInfo.InvariantCulture, $"Item {n}"), repo));
        }

        return ids;
    }

    internal static async Task Stall(GoldenHost host, CancellationToken ct)
    {
        await ViewScenarios.CatchUp(host, ct);   // the first build finishes, so the stall is one of a running projector
        await ViewScenarios.Append(host, EventTypes.ItemCreated, "not an item", "item.created:unreadable", ct);
        await ViewScenarios.CatchUp(host, ct);
    }

    internal static async Task AppendFor<TPayload>(GoldenHost host, string item, string type, TPayload payload, string key, CancellationToken ct)
    {
        await using var scope = ViewScenarios.Service(host, out var grant);
        var events = scope.ServiceProvider.GetRequiredService<EventFactory>();
        var appended = await scope.ServiceProvider.GetRequiredService<ILedgerStore>().Append(events.Entry(type, payload, key, item), grant, ct);
        _ = appended.Value;
    }
}
