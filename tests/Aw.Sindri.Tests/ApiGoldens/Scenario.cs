using System.Text.Json;
using Aw.Context.Core.Graph;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.Callers;
using Aw.Sindri.Tests.ContextCalls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The steps of one context golden on a started host: the setup calls (open the map-tabs draft, decide, advance the clock) are made and
/// checked, and each scenario returns the body of its last call only. The app's calls carry the "ui" label without a terminal; the
/// protected ones (promote, apply-file) a terminal.
/// </summary>
internal sealed class Scenario(GoldenHost host)
{
    private static readonly string _link = LinkIds.For(new GraphEdge(NodeKey.From("code:src/tabs/TabGroups.ts"), NodeKey.From("feature:tabs.tab-groups"), EdgeType.Implements)).Value;

    /// <summary>The host the scenario runs on.</summary>
    internal GoldenHost RunsOn() => host;

    private (string Name, string Value)[] AppHeaders() => [.. Human(host.Token.Value, client: "ui").Where(h => h.Name != SindriHeaders.Terminal)];

    private (string Name, string Value)[] TerminalHeaders() => Human(host.Token.Value);

    private static string Review(string verb, string? node = null, string? link = null, string? @override = null, string[]? overrides = null, string? path = null) =>
        JsonSerializer.Serialize(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["verb"] = verb,
            ["node"] = node,
            ["link"] = link,
            ["override"] = @override,
            ["overrides"] = overrides,
            ["path"] = path,
        });

    private static JsonElement Json(string body) => JsonDocument.Parse(body).RootElement;

    /// <summary>GET as the app.</summary>
    public async Task<string> Fetch(string route, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return (await host.Http.Ask(Get(route, AppHeaders()))).Body;
    }

    /// <summary>Lets the views follow the ledger (the golden host hosts no projector), then GET as the app: what the profile's approval row says is there.</summary>
    public async Task<string> FetchWithViews(string route, CancellationToken ct)
    {
        await ViewScenarios.CatchUp(host, ct);
        return await Fetch(route, ct);
    }

    /// <summary>
    /// Starts a map build as the human, then GET as the app. A <paramref name="hung"/> build never ends (and is waited for until its first progress line is in the jobs view, so the answer
    /// counts it); the other ends first, leaving a draft and no current map.
    /// </summary>
    public async Task<string> FetchAfterBuild(string route, bool hung, CancellationToken ct)
    {
        var started = await host.Http.Ask(Post("/sindri/context/map/build", "{}", TerminalHeaders()));
        Assert.Equal(System.Net.HttpStatusCode.OK, started.Status);
        if (hung)
        {
            await using var scope = host.App.Services.CreateAsyncScope();
            var jobs = scope.ServiceProvider.GetRequiredService<IJobViews>();
            var waited = System.Diagnostics.Stopwatch.StartNew();
            do
            {
                await ViewScenarios.CatchUp(host, ct);
                var running = await jobs.Running(ct);
                if (running.IsSuccess && running.Value.Any(j => j.Progress.Count > 0))
                {
                    break;
                }

                await Task.Delay(20, ct);
            }
            while (waited.Elapsed < Jobs.JobHost.Hang);
        }
        else
        {
            await Jobs.JobHost.Settle(host, ct);
        }

        await ViewScenarios.CatchUp(host, ct);
        return await Fetch(route, ct);
    }

    /// <summary>QUERY (the HTTP method with a body) a body query as the app (the subgraph route): a read, so the answer is the body of a GET for the same keys.</summary>
    public async Task<string> Query(string route, string json, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return (await host.Http.Ask(CallerRequests.Query(route, json, AppHeaders()))).Body;
    }

    /// <summary>QUERY a body query from a browser page (an Origin header): refused as a command is.</summary>
    public async Task<string> QueryForeignOrigin(string route, string json, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return (await host.Http.Ask(CallerRequests.Query(route, json, [.. AppHeaders(), ("Origin", "http://localhost:3000")]))).Body;
    }

    /// <summary>GET as an agent session of the fixture repo (X-Sindri-Repo): its scope, not the human's.</summary>
    public async Task<string> FetchAsSession(string route, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return (await host.Http.Ask(Get(route, (SindriHeaders.Caller, "session"), (SindriHeaders.Repo, "aw-split-panes")))).Body;
    }

    /// <summary>Imports a second, smaller map (so the first is a retained version), then GET as the app.</summary>
    public async Task<string> FetchAfterSecondMap(string route, CancellationToken ct)
    {
        var file = Path.Combine(host.Home, "second-map.yaml");
        await File.WriteAllTextAsync(file, string.Join('\n', "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:", "  - {key: \"feature:only.in.two\", label: \"Only in two\"}") + "\n", ct);
        await ContextMapApiTests.Import(host, file);
        return await Fetch(route, ct);
    }

    /// <summary>POST map/import as an agent session: still refused before it runs (import keeps the typed confirmation, owner q93, 2026-10-23).</summary>
    public async Task<string> SessionReview(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return (await host.Http.Ask(Post("/sindri/context/map/import", """{"path":"/x/map.yaml"}""", (SindriHeaders.Caller, "session"), (SindriHeaders.Repo, "aw-split-panes")))).Body;
    }

    /// <summary>The draft is built, then GET as the app.</summary>
    public async Task<string> FetchAfterDraft(string route, CancellationToken ct)
    {
        await Draft(ct);
        return await Fetch(route, ct);
    }

    /// <summary>The draft, then the queue.</summary>
    public async Task<string> MapTabsDraft(CancellationToken ct)
    {
        await Draft(ct);
        return await Fetch("/sindri/context/map/review", ct);
    }

    /// <summary>Accepting the model's suggested link.</summary>
    public async Task<string> PostAcceptLink(CancellationToken ct)
    {
        await Draft(ct);
        return await Decide(Review("accept", link: _link));
    }

    /// <summary>Accepting the same link twice: the second finds it decided.</summary>
    public async Task<string> Stale(CancellationToken ct)
    {
        _ = await PostAcceptLink(ct);
        return await Decide(Review("accept", link: _link));
    }

    /// <summary>Accepting the link and dropping the orphan, then undoing both in one call.</summary>
    public async Task<string> PostUndoList(CancellationToken ct)
    {
        await Draft(ct);
        var accepted = Written(await Decide(Review("accept", link: _link)));
        var dropped = Written(await Decide(Review("drop", @override: await Orphan(ct))));
        Advance();
        return await Decide(Review("undo", overrides: [accepted[0], dropped[0]]));
    }

    /// <summary>Dropping the orphaned override.</summary>
    public async Task<string> PostDrop(CancellationToken ct)
    {
        await Draft(ct);
        return await Decide(Review("drop", @override: await Orphan(ct)));
    }

    /// <summary>Repointing the orphaned rename at another feature.</summary>
    public async Task<string> PostRepoint(CancellationToken ct)
    {
        await Draft(ct);
        return await Decide(Review("repoint", node: "feature:tabs.tab-groups", @override: await Orphan(ct)));
    }

    /// <summary>The bulk accept of a rebuilt draft's deterministic proposals.</summary>
    public async Task<string> AcceptDeterministic(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return await Decide(Review("accept-deterministic"));
    }

    /// <summary>Exporting the queue, then applying the file from a terminal: it acts at once, with no challenge.</summary>
    public async Task<string> ApplyFile(CancellationToken ct)
    {
        await Draft(ct);
        var path = Json(await Fetch("/sindri/context/map/review/export", ct)).GetProperty("path").GetString() ?? string.Empty;
        return (await host.Http.Ask(Post("/sindri/context/map/review", Review("apply-file", path: path), TerminalHeaders()))).Body;
    }

    /// <summary>Accepting, undoing, then the history.</summary>
    public async Task<string> AcceptThenUndo(CancellationToken ct)
    {
        await Draft(ct);
        var accepted = Written(await Decide(Review("accept", link: _link)));
        Advance();
        _ = await Decide(Review("undo", @override: accepted[0]));
        return await Fetch("/sindri/context/map/review/history", ct);
    }

    /// <summary>A scoping run's proposals: one surface the map has, one it gets as new, one with no kind (unmapped); the response names each one's standing.</summary>
    public async Task<string> Propose(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        const string Body = """{"run":"golden-run","repo":"aw-split-panes","surfaces":[{"name":"Tab manager","key":"feature:tabs.tab-manager"},{"name":"Zoom","kind":"feature","evidence":[]},{"name":"Something vague"}]}""";
        var proposed = await host.Http.Ask(Post("/sindri/context/map/proposals", Body, AppHeaders()));
        Assert.True(proposed.Status == System.Net.HttpStatusCode.OK, proposed.Body);
        return proposed.Body;
    }

    /// <summary>Promoting with items undecided.</summary>
    public async Task<string> PromoteRefused(CancellationToken ct)
    {
        await Draft(ct);
        return (await host.Http.Ask(Post("/sindri/context/map/promote", "{}", TerminalHeaders()))).Body;
    }

    /// <summary>Promoting once every item is decided: it promotes at once, with no challenge.</summary>
    public async Task<string> Promote(CancellationToken ct)
    {
        await Draft(ct);
        _ = await Decide(Review("accept", link: _link));
        _ = await Decide(Review("drop", @override: await Orphan(ct)));
        return (await host.Http.Ask(Post("/sindri/context/map/promote", "{}", TerminalHeaders()))).Body;
    }

    /// <summary>Opens the draft: one proposal run whose only surface is mapped already, so the draft is a copy of the current map.</summary>
    private async Task Draft(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        const string Body = """{"run":"golden-run","repo":"aw-split-panes","surfaces":[{"name":"Tab manager","key":"feature:tabs.tab-manager"}]}""";
        var drafted = await host.Http.Ask(Post("/sindri/context/map/proposals", Body, AppHeaders()));
        Assert.True(drafted.Status == System.Net.HttpStatusCode.OK, drafted.Body);
        Advance();
    }

    private async Task<string> Orphan(CancellationToken ct) =>
        Json(await Fetch("/sindri/context/map/review", ct)).GetProperty("entries").EnumerateArray()
            .Single(e => e.GetProperty("group").GetString() == "orphaned").GetProperty("override").GetString()!;

    private async Task<string> Decide(string body) => (await host.Http.Ask(Post("/sindri/context/map/review", body, AppHeaders()))).Body;

    private static string[] Written(string body) => [.. Json(body).GetProperty("result").GetProperty("written").EnumerateArray().Select(w => w.GetString() ?? string.Empty)];

    private void Advance() =>
        ((FakeTimeProvider)host.App.Services.GetRequiredService<TimeProvider>()).Advance(TimeSpan.FromMinutes(1));   // why: the golden host's clock stands still, and a decision is ordered after what it decides about by its time
}
