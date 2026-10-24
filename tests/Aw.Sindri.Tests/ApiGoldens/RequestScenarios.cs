using System.Globalization;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Core.Views;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// API P4's golden scenarios (plan §6): the human's list, the request and decision lists, filing a request and deciding a decision. Every scenario
/// starts its own host on <see cref="ItemScenarios.ProfileYaml"/> (two repos), approves it, makes its items and requests through the API, and drives
/// Projector.CatchUp on the fake clock before it reads. Ids are sequential, so every 'ITM-…', 'REQ-…' and 'DEC-…' is stable.
/// </summary>
internal static class RequestScenarios
{
    private const string NoSuchDecision = "DEC-000000000000000000000000000000ff";
    private const string NoSuchItem = "ITM-000000000000000000000000000000ff";

    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("needs-you", "empty"), ("needs-you", "fixture"), ("needs-you", "one-request"), ("needs-you", "stalled"), ("needs-you", "rebuilding"),
        ("needs-you", "large"), ("needs-you", "session-repo"), ("needs-you", "after-decide"),
        ("decisions", "pending"), ("decisions", "decided"), ("decisions", "empty"), ("decisions", "empty-decided"), ("decisions", "voided"),
        ("decisions", "by-item"), ("decisions", "stalled"), ("decisions", "session-other-repo"),
        ("decisions-id", "accepted"), ("decisions-id", "declined"), ("decisions-id", "decided-elsewhere"), ("decisions-id", "voided"),
        ("decisions-id", "not-found"), ("decisions-id", "bad-choice"), ("decisions-id", "bad-id"), ("decisions-id", "refused-session"), ("decisions-id", "while-stalled"),
        ("requests", "list-fixture"), ("requests", "list-empty"), ("requests", "list-state-accepted"), ("requests", "list-by-item"),
        ("requests", "list-session-other-repo"), ("requests", "list-bad-state"),
        ("requests", "filed"), ("requests", "filed-item"), ("requests", "invalid"), ("requests", "invalid-kind"), ("requests", "too-long"),
        ("requests", "too-many"), ("requests", "item-not-found"), ("requests", "item-closed"), ("requests", "refused-origin"),
    ];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    private const string Split = "Split the checkout work into two items.";

    /// <summary>Runs the scenario against <paramref name="host"/> (started on <see cref="ItemScenarios.ProfileYaml"/>) and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        await ViewScenarios.Approve(host, ct);
        return slug switch
        {
            "needs-you" => await NeedsYou(host, scenario, ct),
            "decisions" => await Decisions(host, scenario, ct),
            "decisions-id" => await DecideBody(host, scenario, ct),
            _ when scenario.StartsWith("list-", StringComparison.Ordinal) => await ListRequests(host, scenario, ct),
            _ => await FileBody(host, scenario, ct),
        };
    }

    private static (string Name, string Value)[] Demo => ItemScenarios.Session("demo");

    private static Task<(string Request, string Decision)> Filed(GoldenHost host, CancellationToken ct, string text = Split, string kind = "rescope", string? item = null, string repo = "demo") =>
        RequestCalls.Filed(host, text, ItemScenarios.Session(repo), ct, kind, item);

    private static async Task<string> NeedsYou(GoldenHost host, string scenario, CancellationToken ct)
    {
        switch (scenario)
        {
            case "fixture":
                return await Fixture(host, ct);
            case "stalled":
                _ = await Filed(host, ct);
                await ItemScenarios.Stall(host, ct);
                break;
            case "rebuilding":
                _ = await Filed(host, ct);
                host.App.Services.GetRequiredService<IProjectorControl>().RequestRebuild();
                return (await host.Http.Ask(Get("/sindri/needs-you", ItemScenarios.Human(host)))).Body;
            case "large":
                await Large(host, ct);
                break;
            case "session-repo":
                _ = await Filed(host, ct, "Mine, in demo.");
                _ = await Filed(host, ct, "Theirs, in other.", repo: "other");
                await Failed(host, ct);
                return await RequestCalls.Read(host, "/sindri/needs-you", Demo, ct);
            case "after-decide":
                var decision = (await Filed(host, ct)).Decision;
                _ = await Filed(host, ct, "Add a task for the logout flow.", "add-task");
                _ = await RequestCalls.Decide(host, decision, "accept", note: null, ItemScenarios.Human(host), ct);
                break;
            case "one-request":
                _ = await Filed(host, ct);
                break;
            default:
                break;   // 'empty'
        }

        return await RequestCalls.Read(host, "/sindri/needs-you", ItemScenarios.Human(host), ct);
    }

    /// <summary>One decision, one failure and one profile row, in that order (UI lane: urgency pulse, colour, calm). The request is filed last, so the order is the urgency's, not recency.</summary>
    private static async Task<string> Fixture(GoldenHost host, CancellationToken ct)
    {
        await Failed(host, ct);
        await ViewScenarios.Drift(host, ItemScenarios.ProfileYaml, ct);
        _ = await Filed(host, ct);
        var body = await RequestCalls.Read(host, "/sindri/needs-you", ItemScenarios.Human(host), ct);
        Assert.Equal(["decision", "failure", "profile"], JsonDocument.Parse(body).RootElement.GetProperty("rows").EnumerateArray().Select(r => r.GetProperty("kind").GetString()), StringComparer.Ordinal);
        return body;
    }

    private static async Task Large(GoldenHost host, CancellationToken ct)
    {
        string[] kinds = ["rescope", "add-task", "note", "handback"];
        for (var n = 0; n < 12; n++)
        {
            var repo = n % 2 == 0 ? "demo" : "other";
            _ = await Filed(host, ct, string.Create(CultureInfo.InvariantCulture, $"Request {n + 1} for {repo}."), kinds[n % kinds.Length], repo: repo);
        }

        await Failed(host, ct);
        await ViewScenarios.Drift(host, ItemScenarios.ProfileYaml, ct);
    }

    private static Task Failed(GoldenHost host, CancellationToken ct) =>
        ViewScenarios.Append(host, EventTypes.ServiceRan, new ServiceRan(CommandId.From(new Guid(0xa1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)), ServiceName.From("heartbeat"), KnownInstants.Epoch, ServiceOutcome.Failed, "SND-SCHED-001"), "service.ran:golden", ct);

    private static async Task<string> Decisions(GoldenHost host, string scenario, CancellationToken ct)
    {
        var route = "/sindri/decisions";
        switch (scenario)
        {
            case "pending":
                _ = await Filed(host, ct);
                _ = await Filed(host, ct, "Add a task for the logout flow.", "add-task");
                break;
            case "decided":
                _ = await RequestCalls.Decide(host, (await Filed(host, ct)).Decision, "accept", "Good plan.", ItemScenarios.Human(host), ct);
                route += "?pending=false";
                break;
            case "empty-decided":
                _ = await Filed(host, ct);
                route += "?pending=false";
                break;
            case "voided":
                var item = await ItemScenarios.Create(host, "Try the queue", "demo");
                _ = await Filed(host, ct, item: item);
                _ = await Close(host, item, ct);
                break;
            case "by-item":
                route += "?item=" + await AboutItemAndNothing(host, ct);
                break;
            case "stalled":
                _ = await Filed(host, ct);
                await ItemScenarios.Stall(host, ct);
                break;
            case "session-other-repo":
                _ = await Filed(host, ct, "In other.", repo: "other");
                return await RequestCalls.Read(host, route, Demo, ct);
            default:
                break;   // 'empty'
        }

        return await RequestCalls.Read(host, route, ItemScenarios.Human(host), ct);
    }

    /// <summary>Makes an item with one request about it and one about nothing; returns the item's id.</summary>
    private static async Task<string> AboutItemAndNothing(GoldenHost host, CancellationToken ct)
    {
        var item = await ItemScenarios.Create(host, "Try the queue", "demo");
        _ = await Filed(host, ct, "About the item.", item: item);
        _ = await Filed(host, ct, "About nothing.");
        return item;
    }

    private static async Task<string> Close(GoldenHost host, string item, CancellationToken ct)
    {
        await ViewScenarios.CatchUp(host, ct);
        var answer = await host.Http.Ask(Post($"/sindri/items/{item}/close", """{"reason":"done","itemVersion":1}""", ItemScenarios.Human(host)));
        await ViewScenarios.CatchUp(host, ct);
        return answer.Body;
    }

    private static async Task<string> DecideBody(GoldenHost host, string scenario, CancellationToken ct)
    {
        var human = ItemScenarios.Human(host);
        if (string.Equals(scenario, "not-found", StringComparison.Ordinal))
        {
            return (await RequestCalls.Decide(host, NoSuchDecision, "accept", note: null, human, ct)).Body;
        }

        if (string.Equals(scenario, "bad-id", StringComparison.Ordinal))
        {
            return (await RequestCalls.Decide(host, "not-a-decision", "accept", note: null, human, ct)).Body;
        }

        var item = scenario == "voided" ? await ItemScenarios.Create(host, "Try the queue", "demo") : null;
        var decision = (await Filed(host, ct, item: item)).Decision;
        var pick = scenario switch
        {
            "declined" => new Pick("decline", "Not now.", human),
            "decided-elsewhere" => await Elsewhere(host, decision, human, ct),
            "bad-choice" => new Pick("maybe", Note: null, human),
            "refused-session" => new Pick("accept", Note: null, Demo),
            _ => new Pick("accept", "Good plan.", human),   // 'accepted', 'voided', 'while-stalled'
        };
        if (scenario == "voided")
        {
            _ = await Close(host, item!, ct);
        }
        else if (scenario == "while-stalled")
        {
            await ItemScenarios.Stall(host, ct);
        }

        return (await RequestCalls.Decide(host, decision, pick.Choice, pick.Note, pick.Caller, ct)).Body;
    }

    /// <summary>The first human accepts; the scenario's own decide then declines, and loses.</summary>
    private static async Task<Pick> Elsewhere(GoldenHost host, string decision, (string Name, string Value)[] human, CancellationToken ct)
    {
        _ = await RequestCalls.Decide(host, decision, "accept", note: null, human, ct);
        return new Pick("decline", Note: null, human);
    }

    private static async Task<string> ListRequests(GoldenHost host, string scenario, CancellationToken ct)
    {
        var route = "/sindri/requests";
        switch (scenario)
        {
            case "list-fixture":
                _ = await Filed(host, ct);
                break;
            case "list-state-accepted":
                var decision = (await Filed(host, ct)).Decision;
                _ = await Filed(host, ct, "Still open.", "note");
                _ = await RequestCalls.Decide(host, decision, "accept", note: null, ItemScenarios.Human(host), ct);
                route += "?state=accepted";
                break;
            case "list-by-item":
                route += "?item=" + await AboutItemAndNothing(host, ct);
                break;
            case "list-session-other-repo":
                _ = await Filed(host, ct, "In other.", repo: "other");
                return await RequestCalls.Read(host, route, Demo, ct);
            case "list-bad-state":
                route += "?state=sleeping";
                break;
            default:
                break;   // 'list-empty'
        }

        return await RequestCalls.Read(host, route, ItemScenarios.Human(host), ct);
    }

    private static async Task<string> FileBody(GoldenHost host, string scenario, CancellationToken ct)
    {
        var item = scenario switch
        {
            "filed-item" or "item-closed" => await ItemScenarios.Create(host, "Try the queue", "demo"),
            "item-not-found" => NoSuchItem,
            _ => null,
        };
        if (scenario == "item-closed")
        {
            _ = await Close(host, item!, ct);
        }

        if (scenario == "too-many")
        {
            for (var n = 0; n < RequestRules.MaxOpenPerRepo; n++)
            {
                _ = await Filed(host, ct, string.Create(CultureInfo.InvariantCulture, $"Request {n + 1}."), "note");
            }
        }

        var (kind, text) = scenario switch
        {
            "invalid" => ("rescope", "   "),
            "invalid-kind" => ("bogus", "Split it."),
            "too-long" => ("rescope", new string('x', RequestRules.TextMax + 1)),
            "too-many" => ("note", "One too many."),
            _ => ("rescope", Split),
        };
        var headers = scenario == "refused-origin" ? Demo.Append(("Origin", "http://localhost:3000")).ToArray() : Demo;
        return (await RequestCalls.File(host, RequestCalls.Body(kind, text, item), headers, ct)).Body;
    }

    private sealed record Pick(string Choice, string? Note, (string Name, string Value)[] Caller);
}
