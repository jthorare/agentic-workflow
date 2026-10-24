using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Host;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// API P5's goldens (plan §6): GET /events (the frames' data as a JSON array of public events, and the refusals) and GET /ledger.
/// Every scenario runs on <see cref="ItemScenarios.ProfileYaml"/> (two repos): it approves the profile, makes an item in each repo through
/// the API, appends a heartbeat and an item whose title carries terminal control sequences, then reads as the scenario's caller.
/// </summary>
internal static class StreamScenarios
{
    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("events", "types-service"), ("events", "item-filter"), ("events", "session-repo"), ("events", "session-no-repo"), ("events", "ansi-escaped"),
        ("events", "after-past-head"), ("events", "bad-types"), ("events", "streams-full"),
        ("ledger", "human-all"), ("ledger", "session-public"), ("ledger", "session-repo"), ("ledger", "type-filter"), ("ledger", "item-filter"),
        ("ledger", "paged"), ("ledger", "max-over-10000"), ("ledger", "after-past-head"), ("ledger", "ansi-escaped"),
    ];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Runs the scenario against <paramref name="host"/> (started on <see cref="ItemScenarios.ProfileYaml"/>) and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        await ViewScenarios.Approve(host, ct);
        var demo = await ItemScenarios.Create(host, "In demo", "demo");
        _ = await ItemScenarios.Create(host, "In other", "other");
        await ViewScenarios.Append(host, "service.heartbeat", new Heartbeat(host.Now(), host.Now()), "service.heartbeat:1:1", ct);
        if (scenario == "ansi-escaped")
        {
            await ItemScenarios.AppendFor(host, "ITM-" + new string('a', 32), EventTypes.ItemCreated, Ansi(), "item.created:ansi", ct);
        }

        await ViewScenarios.CatchUp(host, ct);   // an item named by id or alias resolves through the views
        return slug == "events" ? await EventsBody(host, scenario, demo, ct) : await LedgerBody(host, scenario, demo);
    }

    private static ItemCreated Ansi() =>
        new(CommandId.From(new Guid(99, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)), ItemId.From("ITM-" + new string('a', 32)),
            "Fix \u001b]8;;http://example.org\u0007link\u001b]8;;\u0007 \u001b[31mred\u001b[0m", RepoName.From("demo"), Link: null, Body: "private body", Owner: null);

    private static async Task<string> EventsBody(GoldenHost host, string scenario, string demo, CancellationToken ct)
    {
        var human = ItemScenarios.Human(host);
        return scenario switch
        {
            "types-service" => await Frames(host, "/sindri/events?types=service.*", human, ct),
            "item-filter" => await Frames(host, "/sindri/events?item=" + demo, human, ct),
            "session-repo" => await Frames(host, "/sindri/events", ItemScenarios.Session("demo"), ct),
            "session-no-repo" => await Frames(host, "/sindri/events", [(SindriHeaders.Caller, "session"), (SindriHeaders.Client, "cli")], ct),
            "ansi-escaped" => await Frames(host, "/sindri/events?types=item.created", human, ct),
            "after-past-head" => (await host.Http.Ask(Get("/sindri/events?after=99999", human))).Body,
            "bad-types" => (await host.Http.Ask(Get("/sindri/events?types=Bad%20Glob", human))).Body,
            _ => await StreamsFull(host, human, ct),
        };
    }

    private static async Task<string> LedgerBody(GoldenHost host, string scenario, string demo)
    {
        var human = ItemScenarios.Human(host);
        var (route, headers) = scenario switch
        {
            "human-all" => ("/sindri/ledger", human),
            "session-public" => ("/sindri/ledger", [(SindriHeaders.Caller, "session"), (SindriHeaders.Client, "cli")]),
            "session-repo" => ("/sindri/ledger", ItemScenarios.Session("demo")),
            "type-filter" => ("/sindri/ledger?type=item.*", human),
            "item-filter" => ("/sindri/ledger?item=" + demo, human),
            "paged" => ("/sindri/ledger?after=2&max=3", human),
            "max-over-10000" => ("/sindri/ledger?max=10001", human),
            "after-past-head" => ("/sindri/ledger?after=99999", human),
            _ => ("/sindri/ledger?type=item.created", human),   // 'ansi-escaped'
        };
        return (await host.Http.Ask(Get(route, headers))).Body;
    }

    /// <summary>The 65th stream's 429, after sixty-four are open.</summary>
    private static async Task<string> StreamsFull(GoldenHost host, (string Name, string Value)[] headers, CancellationToken ct)
    {
        var open = new List<(HttpResponseMessage Response, Stream Body)>();
        try
        {
            for (var i = 0; i < StreamLimits.Total; i++)
            {
                var response = await host.Http.SendAsync(Get("/sindri/events?after=1", headers), HttpCompletionOption.ResponseHeadersRead, ct);
                open.Add((response, await response.Content.ReadAsStreamAsync(ct)));
            }

            return (await host.Http.Ask(Get("/sindri/events", headers))).Body;
        }
        finally
        {
            foreach (var (response, body) in open)
            {
                await body.DisposeAsync();
                response.Dispose();
            }
        }
    }

    /// <summary>The data of each frame the stream sends for the request, as a JSON array: reads until the stream has been quiet for a moment (the clock is frozen, so no ping comes).</summary>
    internal static async Task<string> Frames(GoldenHost host, string route, (string Name, string Value)[] headers, CancellationToken ct)
    {
        using var response = await host.Http.SendAsync(Get(route, headers), HttpCompletionOption.ResponseHeadersRead, ct);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(ct));
        var frames = new JsonArray();
        var wait = TimeSpan.FromSeconds(10);   // the first frame may be slow on a loaded box; later ones come at once
        while (true)
        {
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limit.CancelAfter(wait);
            string? line;
            try
            {
                line = await reader.ReadLineAsync(limit.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                break;
            }

            if (line is null)
            {
                break;
            }

            if (line.StartsWith("data: ", StringComparison.Ordinal))
            {
                frames.Add(JsonNode.Parse(line["data: ".Length..]));
                wait = TimeSpan.FromMilliseconds(400);
            }
        }

        return frames.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }
}
