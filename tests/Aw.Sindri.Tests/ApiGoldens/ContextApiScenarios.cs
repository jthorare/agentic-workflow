using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.WholeMap;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Extract;
using Aw.Context.Testing.Fixtures;
using Aw.Context.Testing.Map;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Infrastructure.Locks;
using Aw.Sindri.Tests.ContextCalls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The context routes' goldens for the app (S-4 UI contract, D11): each answer of the lenses, file map, map review, history,
/// promote, map status and node routes, and each refusal the app maps (003, 037, 044, 045, 046, 047), on a host over the small
/// MapTabsRepo (a fixed sha) with the tabs map imported. The map-tabs draft is one POST map/proposals (a copy
/// of current); the bulk accept runs over the split-panes fixture repo and a rebuild with fake clusters. The author is "golden",
/// so override ids and "who" are the same on every machine.
/// </summary>
internal static class ContextApiScenarios
{
    private const string Repo = MapTabsRepo.Repo;
    private const string Anchor = "feature%3Atabs.tab-manager";
    private const string SplitPane = "feature%3Alayout.split-pane";

    private const string ThreeKeys = "nodes=feature%3Atabs.tab-manager&nodes=feature%3Atabs.tab-groups&nodes=feature%3Anope.x";

    /// <summary>The QUERY body: a mixed set of four keys (a surface, a code file, a decision, one absent), not the GET's three feature keys.</summary>
    internal const string SubgraphBody = """{"nodes":["surface:command/switch-tab","code:src/tabs/TabManager.ts","decision:d036352234b17baf9a7b2e7cffef599bdc9c8209e92c389d2bcbb919c74c162b","feature:nope.x"]}""";   // a real mixed set: a surface, a code file, a decision, one absent key

    /// <summary>The QUERY body of the search golden: a word only a decision's quote holds.</summary>
    internal const string SearchBody = """{"q":"single","limit":5}""";

    private static string Subgraph(string query) => "/sindri/context/subgraph?" + query;

    private static string WholeRoute(string? query = null) => "/sindri/context/map/whole" + (query is null ? string.Empty : "?" + query);

    private delegate Task<string> Run(Scenario scenario, CancellationToken ct);


    private const string CodeAnchor = "feature%3Asharing";

    private static string CodeRoute(string? second = null) => $"/sindri/context/code?anchor={CodeAnchor}" + (second is null ? string.Empty : $"&anchor={second}");

    private static readonly (string Slug, string Scenario, Run Body)[] _table =
    [
        ("context-lenses", "map-tabs", async (s, ct) => await s.Fetch($"/sindri/context/lenses?anchor={Anchor}", ct)),
        ("context-lenses", "depth-1", async (s, ct) => await s.Fetch($"/sindri/context/lenses?anchor={Anchor}&depth=1", ct)),
        ("context-lenses", "unknown-anchor-003", async (s, ct) => await s.Fetch("/sindri/context/lenses?anchor=feature%3Atabs.nope", ct)),
        ("context-lenses", "ambiguous-037", async (s, ct) => await s.Fetch("/sindri/context/lenses?anchor=Layout", ct)),
        ("context-lenses", "no-draft-044", async (s, ct) => await s.Fetch($"/sindri/context/lenses?anchor={Anchor}&draft=true", ct)),
        ("context-lenses", "set-two-anchors", async (s, ct) => await s.Fetch($"/sindri/context/lenses?anchor={Anchor}&anchor=feature%3Atabs.tab-groups", ct)),
        ("context-lenses", "set-missing-anchor", async (s, ct) => await s.Fetch($"/sindri/context/lenses?anchor={Anchor}&anchor=feature%3Anope.x", ct)),
        ("context-lenses", "bad-depth-006", async (s, ct) => await s.Fetch($"/sindri/context/lenses?anchor={Anchor}&depth=-1", ct)),
        ("context-lenses", "map-version-v1", async (s, ct) => await s.FetchAfterSecondMap($"/sindri/context/lenses?anchor={Anchor}&mapVersion=v1", ct)),
        ("context-lenses", "map-version-not-retained-120", async (s, ct) => await s.Fetch($"/sindri/context/lenses?anchor={Anchor}&mapVersion=v99", ct)),
        ("context-lenses", "session-set-other-repo", async (s, ct) => await s.FetchAsSession($"/sindri/context/lenses?anchor={Anchor}&anchor=feature%3Aother.thing", ct)),
        ("context-subgraph", "get-three-keys", async (s, ct) => await s.Fetch(Subgraph(ThreeKeys), ct)),
        ("context-subgraph", "query-packet-set", async (s, ct) => await s.Query("/sindri/context/subgraph", SubgraphBody, ct)),
        ("context-subgraph", "depth-2", async (s, ct) => await s.Fetch(Subgraph(ThreeKeys) + "&depth=2", ct)),
        ("context-subgraph", "missing-after-v2", async (s, ct) => await s.FetchAfterSecondMap(Subgraph(ThreeKeys), ct)),
        ("context-subgraph", "session-own-repo", async (s, ct) => await s.FetchAsSession(Subgraph(ThreeKeys) + "&depth=2", ct)),
        ("context-subgraph", "too-many-006", async (s, ct) => await s.Fetch(Subgraph(string.Join('&', Enumerable.Range(0, 21).Select(i => "nodes=feature%3Anope." + i.ToString(System.Globalization.CultureInfo.InvariantCulture)))), ct)),
        ("context-subgraph", "not-retained-120", async (s, ct) => await s.Fetch(Subgraph(ThreeKeys) + "&mapVersion=v99", ct)),
        ("context-subgraph", "query-refused-origin", async (s, ct) => await s.QueryForeignOrigin("/sindri/context/subgraph", SubgraphBody, ct)),
        ("context-search", "get-tab-manager", async (s, ct) => await s.Fetch("/sindri/context/search?q=tab+manager", ct)),
        ("context-search", "query-decision-text", async (s, ct) => await s.Query("/sindri/context/search", SearchBody, ct)),
        ("context-search", "session-own-repo", async (s, ct) => await s.FetchAsSession("/sindri/context/search?q=tab", ct)),
        ("context-search", "empty-q-006", async (s, ct) => await s.Fetch("/sindri/context/search?q=", ct)),
        ("context-search", "limit-201-006", async (s, ct) => await s.Fetch("/sindri/context/search?q=tab&limit=201", ct)),
        ("context-code", "edges-and-lexical", async (s, ct) => await s.Fetch(CodeRoute(), ct)),
        ("context-code", "two-anchors", async (s, ct) => await s.Fetch(CodeRoute("feature%3Aaudit"), ct)),
        ("context-code", "schema-off", async (s, ct) => await s.Fetch(CodeRoute(), ct)),
        ("context-code", "commit-not-held", async (s, ct) => await s.Fetch(CodeRoute(), ct)),
        ("context-code", "map-version-v1", async (s, ct) => await s.FetchAfterSecondMap(CodeRoute() + "&mapVersion=v1", ct)),
        ("context-code", "capped", async (s, ct) => await s.Fetch(CodeRoute(), ct)),
        ("context-code", "deadline-152", async (s, ct) => await s.Fetch(CodeRoute(), ct)),
        ("context-code", "unknown-anchor-003", async (s, ct) => await s.Fetch("/sindri/context/code?anchor=feature%3Anope", ct)),
        ("context-code", "not-retained-120", async (s, ct) => await s.Fetch(CodeRoute() + "&mapVersion=v99", ct)),
        ("context-code", "session-other-repo", async (s, ct) => await s.FetchAsSession(CodeRoute("feature%3Aother.thing"), ct)),
        ("context-history", "split-pane", async (s, ct) => await s.Fetch($"/sindri/context/history?anchor={SplitPane}", ct)),
        ("context-history", "before-cut", async (s, ct) => await s.Fetch($"/sindri/context/history?anchor={SplitPane}&before=2026-07-12T00:00:00Z", ct)),
        ("context-history", "since-window", async (s, ct) => await s.Fetch($"/sindri/context/history?anchor={SplitPane}&since=2026-07-20T00:00:00Z", ct)),
        ("context-history", "two-anchors", async (s, ct) => await s.Fetch($"/sindri/context/history?anchor={SplitPane}&anchor=feature%3Atabs.tab-manager", ct)),
        ("context-history", "missing-anchor", async (s, ct) => await s.Fetch($"/sindri/context/history?anchor={SplitPane}&anchor=feature%3Anope.x", ct)),
        ("context-history", "no-files-thing", async (s, ct) => await s.Fetch("/sindri/context/history?anchor=thing%3Apane-layout", ct)),
        ("context-history", "unknown-anchor-003", async (s, ct) => await s.Fetch("/sindri/context/history?anchor=feature%3Atabs.nope", ct)),
        ("context-history", "bad-window-006", async (s, ct) => await s.Fetch($"/sindri/context/history?anchor={SplitPane}&since=90d", ct)),
        ("context-history", "map-version-v1", async (s, ct) => await s.FetchAfterSecondMap($"/sindri/context/history?anchor={SplitPane}&mapVersion=v1", ct)),
        ("context-history", "not-retained-120", async (s, ct) => await s.Fetch($"/sindri/context/history?anchor={SplitPane}&mapVersion=v99", ct)),
        ("context-history", "session-other-repo", async (s, ct) => await s.FetchAsSession($"/sindri/context/history?anchor={SplitPane}&anchor=feature%3Aother.thing", ct)),
        ("context-lenses", "hops-component", async (s, ct) => await s.Fetch("/sindri/context/lenses?anchor=feature%3Ashare.sharing&depth=1&hops=state,dependency,flow,component", ct)),
        ("context-lenses", "hops-component-skipped", async (s, ct) => await s.Fetch("/sindri/context/lenses?anchor=feature%3Autil.u0&depth=1&hops=component", ct)),
        ("context-lenses", "hops-bogus-140", async (s, ct) => await s.Fetch("/sindri/context/lenses?anchor=feature%3Ashare.sharing&hops=bogus", ct)),
        ("context-lenses", "query-set-two-anchors", async (s, ct) => await s.Query("/sindri/context/lenses", """{"anchors":["feature:tabs.tab-manager","feature:tabs.tab-groups"]}""", ct)),
        ("context-lenses", "query-no-anchor-006", async (s, ct) => await s.Query("/sindri/context/lenses", """{"anchors":[]}""", ct)),
        ("context-lenses", "query-session-set-other-repo", async (s, ct) => await s.QueryAsSession("/sindri/context/lenses", """{"anchors":["feature:tabs.tab-manager","feature:other.thing"]}""", ct)),
        ("context-code", "query-two-anchors", async (s, ct) => await s.Query("/sindri/context/code", """{"anchors":["feature:sharing","feature:nope"]}""", ct)),
        ("context-code", "query-no-anchor-006", async (s, ct) => await s.Query("/sindri/context/code", """{"anchors":[]}""", ct)),
        ("context-history", "query-two-anchors", async (s, ct) => await s.Query("/sindri/context/history", """{"anchors":["feature:layout.split-pane","feature:tabs.tab-manager"]}""", ct)),
        ("context-history", "query-no-anchor-006", async (s, ct) => await s.Query("/sindri/context/history", """{"anchors":[]}""", ct)),
        ("context-whole", "nodes-small", async (s, ct) => await s.Fetch(WholeRoute(), ct)),
        ("context-whole", "areas-over-budget", async (s, ct) => await s.Fetch(WholeRoute(), ct)),
        ("context-whole", "area-open", async (s, ct) => await s.Fetch(WholeRoute("area=feature%3Atabs.tab-groups"), ct)),
        ("context-whole", "area-truncated", async (s, ct) => await s.Fetch(WholeRoute("area=feature%3Atabs.tab-groups"), ct)),
        ("context-whole", "area-unknown-200", async (s, ct) => await s.Fetch(WholeRoute("area=feature%3Anope"), ct)),
        ("context-whole", "locate", async (s, ct) => await s.Fetch(WholeRoute("locate=surface%3Acommand%2Fswitch-tab&locate=feature%3Anope.x"), ct)),
        ("context-whole", "draft", async (s, ct) => await s.FetchAfterDraft(WholeRoute("draft=true"), ct)),
        ("context-whole", "no-map-030", async (s, ct) => await s.Fetch(WholeRoute(), ct)),
        ("context-whole", "no-map-never-run", async (s, ct) => await s.FetchWithViews(WholeRoute(), ct)),
        ("context-whole", "no-map-approved", async (s, ct) => await s.FetchAfterBuild(WholeRoute(), hung: false, ct)),
        ("context-whole", "building-in-progress", async (s, ct) => await s.FetchAfterBuild(WholeRoute(), hung: true, ct)),
        ("context-whole", "no-draft-044", async (s, ct) => await s.Fetch(WholeRoute("draft=true"), ct)),
        ("context-whole", "map-version-v1", async (s, ct) => await s.FetchAfterSecondMap(WholeRoute("mapVersion=v1"), ct)),
        ("context-whole", "map-version-not-retained-120", async (s, ct) => await s.Fetch(WholeRoute("mapVersion=v99"), ct)),
        ("context-whole", "bad-detail-006", async (s, ct) => await s.Fetch(WholeRoute("detail=everything"), ct)),
        ("context-whole", "session-own-repo", async (s, ct) => await s.FetchAsSession(WholeRoute(), ct)),
        ("context-whole", "session-other-repo-045", async (s, ct) => await s.FetchAsSession(WholeRoute("repo=elsewhere"), ct)),
        ("context-whole", "concept-open", async (s, ct) => await s.Fetch(WholeRoute("concept=concept%3Ascheduling"), ct)),
        ("context-whole", "concept-other", async (s, ct) => await s.Fetch(WholeRoute("concept=concept%3Aother"), ct)),
        ("context-whole", "concept-unknown-200", async (s, ct) => await s.Fetch(WholeRoute("concept=concept%3Anope"), ct)),
        ("context-whole", "concept-and-area-006", async (s, ct) => await s.Fetch(WholeRoute("concept=concept%3Abilling&area=feature%3Ashifts"), ct)),
        ("context-concepts", "concept-map", async (s, ct) => await s.Fetch("/sindri/context/concepts", ct)),
        ("context-concepts", "filtered-invoices", async (s, ct) => await s.Fetch("/sindri/context/concepts?q=invoices", ct)),
        ("context-concepts", "first-page", async (s, ct) => await s.Fetch("/sindri/context/concepts?limit=1", ct)),
        ("context-concepts", "second-page", async (s, ct) => await s.Fetch("/sindri/context/concepts?limit=1&cursor=v1%3A1", ct)),
        ("context-concepts", "bad-cursor-006", async (s, ct) => await s.Fetch("/sindri/context/concepts?cursor=v9%3A1", ct)),
        ("context-concepts", "bad-limit-006", async (s, ct) => await s.Fetch("/sindri/context/concepts?limit=0", ct)),
        ("context-concepts", "no-concepts-empty", async (s, ct) => await s.Fetch("/sindri/context/concepts", ct)),
        ("context-concepts", "no-map-unavailable", async (s, ct) => await s.Fetch("/sindri/context/concepts", ct)),
        ("context-concepts", "session-own-repo", async (s, ct) => await s.FetchAsSession("/sindri/context/concepts", ct)),
        ("context-concepts-id-members", "scheduling", async (s, ct) => await s.Fetch("/sindri/context/concepts/concept%3Ascheduling/members", ct)),
        ("context-concepts-id-members", "other", async (s, ct) => await s.Fetch("/sindri/context/concepts/concept%3Aother/members", ct)),
        ("context-concepts-id-members", "first-page", async (s, ct) => await s.Fetch("/sindri/context/concepts/concept%3Ascheduling/members?limit=1", ct)),
        ("context-concepts-id-members", "unknown-003", async (s, ct) => await s.Fetch("/sindri/context/concepts/concept%3Anope/members", ct)),
        ("context-eval", "map-tabs", async (s, ct) => await s.Fetch("/sindri/context/eval/map", ct)),
        ("context-filemap", "root", async (s, ct) => await s.Fetch($"/sindri/context/filemap?repo={Repo}", ct)),
        ("context-filemap", "src", async (s, ct) => await s.Fetch($"/sindri/context/filemap?repo={Repo}&path=src/", ct)),
        ("context-filemap", "src-tabs", async (s, ct) => await s.Fetch($"/sindri/context/filemap?repo={Repo}&path=src/tabs/", ct)),
        ("context-filemap", "src-unmapped", async (s, ct) => await s.Fetch($"/sindri/context/filemap?repo={Repo}&path=src/tabs/&unmapped=true", ct)),
        ("context-filemap", "repo-outside-045", async (s, ct) => await s.Fetch("/sindri/context/filemap?repo=elsewhere", ct)),
        ("context-filemap", "no-draft-044", async (s, ct) => await s.Fetch($"/sindri/context/filemap?repo={Repo}&draft=true", ct)),
        ("context-map", "map-tabs-anchor", async (s, ct) => await s.Fetch($"/sindri/context/map?anchor={Anchor}", ct)),
        ("context-map-publish", "published", async (s, ct) => await s.Fetch("/sindri/context/map/publish", ct)),
        ("context-query", "diff-spl-103", async (s, ct) => await s.Fetch($"/sindri/context/query?diff={DiffRange(s)}&repo={FixtureMap.Repo}", ct)),
        ("context-query", "diff-unknown-sha-110", async (s, ct) => await s.Fetch($"/sindri/context/query?diff={_unknownBase}..{HeadOf(s)}&repo={FixtureMap.Repo}", ct)),
        ("context-query", "diff-spl-103-hops-component", async (s, ct) => await s.Fetch($"/sindri/context/query?diff={DiffRange(s)}&repo={FixtureMap.Repo}&hops=component", ct)),
        ("context-query", "hops-component-node", async (s, ct) => await s.Fetch("/sindri/context/query?node=feature%3Autil.u0&depth=1&hops=component", ct)),
        ("context-node", "tab-manager", async (s, ct) => await s.Fetch($"/sindri/context/node?key={Anchor}", ct)),
        ("context-map-review", "no-draft-044", async (s, ct) => await s.Fetch("/sindri/context/map/review", ct)),
        ("context-map-review", "session-accept", async (s, ct) => await s.SessionReview(ct)),
        ("context-map-review", "map-tabs-draft", async (s, ct) => await s.MapTabsDraft(ct)),
        ("context-map-review", "post-accept-link", async (s, ct) => await s.PostAcceptLink(ct)),
        ("context-map-review", "post-undo-list", async (s, ct) => await s.PostUndoList(ct)),
        ("context-map-review", "post-drop", async (s, ct) => await s.PostDrop(ct)),
        ("context-map-review", "post-repoint", async (s, ct) => await s.PostRepoint(ct)),
        ("context-map-review", "stale-047", async (s, ct) => await s.Stale(ct)),
        ("context-map-review", "post-accept-deterministic", async (s, ct) => await s.AcceptDeterministic(ct)),
        ("context-map-review", "post-apply-file", async (s, ct) => await s.ApplyFile(ct)),
        ("context-map-review-history", "after-accept-undo", async (s, ct) => await s.AcceptThenUndo(ct)),
        ("context-map-proposals", "map-tabs-run", async (s, ct) => await s.Propose(ct)),
        ("context-map-promote", "refused-046", async (s, ct) => await s.PromoteRefused(ct)),
        ("context-map-promote", "promoted", async (s, ct) => await s.Promote(ct)),
    ];

    /// <summary>Every (slug, scenario) pair of the context goldens.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> Rows { get; } = [.. _table.Select(t => (t.Slug, t.Scenario))];

    /// <summary>Whether the route slug belongs to the context routes.</summary>
    public static bool Handles(string slug) => slug.StartsWith("context-", StringComparison.Ordinal);

    /// <summary>Starts the host the scenario runs on, with the map imported (the bulk accept: a rebuilt draft over the fixture repo).</summary>
    public static async Task<GoldenHost> Start(string slug, string scenario, CancellationToken ct)
    {
        if (slug == "context-query" && scenario != "hops-component-node")
        {
            return await StartDiff(ct);
        }

        if (slug == "context-code")
        {
            return await StartCode(scenario, ct);
        }

        if (slug == "context-history")
        {
            return await StartHistory(ct);
        }

        var bulk = (slug, scenario) is ("context-map-review", "post-accept-deterministic");
        var locks = Directory.CreateTempSubdirectory("aw-golden-locks-");
        IDisposable repo = bulk ? FixtureRepo.Build() : MapTabsRepo.Build();
        var path = repo is FixtureRepo fixture ? fixture.Path : ((MapTabsRepo)repo).Path;
        var host = await ContextHosts.Approved(path, sessions: true, ct, Wiring(Path.Combine(locks.FullName, "heavy-job.lock"), scenario), bulk ? "split-panes" : Repo);
        host.Own(repo);
        host.Own(new Deleted(locks.FullName));
        if (bulk)
        {
            await Rebuild(host, ct);
        }
        else if (!StartsWithoutMap(slug, scenario))
        {
            await ContextMapApiTests.Import(host, ImportFile(host, slug, scenario));
        }

        return host;
    }

    // The whole-map answers for "no map yet" run on a host that imported nothing.
    private static bool StartsWithoutMap(string slug, string scenario) =>
        (string.Equals(slug, "context-whole", StringComparison.Ordinal) && new[] { "no-map-030", "no-map-never-run", "no-map-approved", "building-in-progress" }.Contains(scenario, StringComparer.Ordinal))
        || (string.Equals(slug, "context-concepts", StringComparison.Ordinal) && string.Equals(scenario, "no-map-unavailable", StringComparison.Ordinal));

    // The code goldens read the CodeLensRepo (a fixed sha); the map names no commit, so the lexical search reads HEAD, except where a scenario says otherwise.
    private static async Task<GoldenHost> StartCode(string scenario, CancellationToken ct)
    {
        var repo = CodeLensRepo.Build(extraTests: scenario == "capped" ? 105 : 0);
        var extra = scenario switch
        {
            "schema-off" => "  code: { schemaGlobs: [] }\n",
            "deadline-152" => "  deadline: 100ms\n  code: { deadline: 100ms }\n",   // the lexical search reads context.code.deadline (#553)
            _ => null,
        };
        Action<IServiceCollection>? wire = scenario == "deadline-152" ? HangingCodeSearch.Install : null;   // a search that never answers: the 100ms code deadline decides
        var host = await ContextHosts.Approved(repo.Path, sessions: true, ct, wire, CodeLensRepo.Repo, contextExtra: extra);
        host.Own(repo);
        var commit = scenario == "commit-not-held" ? new string('f', 40) : null;
        await ContextMapApiTests.Import(host, CodeLensRepo.WriteMap(host.Home, CodeLensRepo.MapYaml(commit)));
        return host;
    }

    // The diff goldens read PR 3 of the split-panes fixture repo, whose shas are the same on every machine.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<GoldenHost, string> _diffRanges = [];

    private static readonly string _unknownBase = new('1', 40);

    private static string HeadOf(Scenario scenario) => DiffRange(scenario)[(DiffRange(scenario).IndexOf("..", StringComparison.Ordinal) + 2)..];

    private static string DiffRange(Scenario scenario) => _diffRanges.TryGetValue(scenario.RunsOn(), out var range) ? range : throw new InvalidOperationException("not a diff scenario");

    private static async Task<GoldenHost> StartDiff(CancellationToken ct)
    {
        var repo = FixtureRepo.Build();
        var host = await ContextHosts.Approved(repo, sessions: true, ct, name: FixtureMap.Repo);
        host.Own(repo);
        await ContextMapApiTests.Import(host);
        var merge = repo.PullRequests.Single(p => p.Number == 3).MergeCommit;
        _diffRanges.Add(host, $"{Rev(repo, merge + "^1")}..{Rev(repo, merge + "^2")}");
        return host;
    }

    // The history goldens read the five merged pull requests of the same fixture repo: committer dates and shas are fixed.
    private static async Task<GoldenHost> StartHistory(CancellationToken ct)
    {
        var repo = FixtureRepo.Build();
        var host = await ContextHosts.Approved(repo, sessions: true, ct, name: FixtureMap.Repo);
        host.Own(repo);
        await ContextMapApiTests.Import(host);
        return host;
    }

    private static string Rev(FixtureRepo repo, string rev)
    {
        var start = new System.Diagnostics.ProcessStartInfo("git") { RedirectStandardOutput = true, UseShellExecute = false };
        foreach (var arg in (string[])["-C", repo.Path, "rev-parse", rev])
        {
            start.ArgumentList.Add(arg);
        }

        using var git = System.Diagnostics.Process.Start(start)!;   // why: git is on the test machine's PATH
        var sha = git.StandardOutput.ReadToEnd().Trim();
        git.WaitForExit();
        return sha;
    }

    /// <summary>Runs the scenario and returns the body to compare (a scenario's setup steps are not part of it).</summary>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        var run = _table.Single(t => t.Slug == slug && t.Scenario == scenario).Body;
        return await run(new Scenario(host), ct);
    }

    private static string ImportFile(GoldenHost host, string slug, string scenario)
    {
        if (scenario.StartsWith("hops-", StringComparison.Ordinal))
        {
            return HopsMap(host.Home);
        }

        if ((slug.StartsWith("context-concepts", StringComparison.Ordinal) && !string.Equals(scenario, "no-concepts-empty", StringComparison.Ordinal)) || scenario.StartsWith("concept-", StringComparison.Ordinal))
        {
            return ConceptMapFile.Write(host.Home);   // the tabs map has no concept: its own scenario reads that answer
        }

        if ((slug, scenario) is not ("context-lenses", "ambiguous-037"))
        {
            return MapTabsRepo.WriteMap(host.Home);
        }

        var file = Path.Combine(host.Home, "ambiguous-map.yaml");
        File.WriteAllText(file, string.Join('\n', "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
            "  - {key: \"feature:layout.a\", label: \"Layout\"}", "  - {key: \"feature:layout.b\", label: \"Layout\"}") + "\n");
        return file;
    }

    /// <summary>Two features sharing one confirmed code unit, and six features sharing a utility over the default fan-out cap of 5.</summary>
    private static string HopsMap(string home)
    {
        var nodes = new List<string>
        {
            "  - {key: \"feature:share.sharing\", label: \"Sharing\"}",
            "  - {key: \"feature:share.export\", label: \"Export\"}",
            "  - {key: \"code:src/share/Share.ts\", label: \"Share.ts\"}",
            "  - {key: \"code:src/util/format.ts\", label: \"format.ts\"}",
        };
        var edges = new List<string>
        {
            "  - {from: \"code:src/share/Share.ts\", to: \"feature:share.sharing\", type: implements}",
            "  - {from: \"code:src/share/Share.ts\", to: \"feature:share.export\", type: implements}",
        };
        for (var i = 0; i < 51; i++)
        {
            var key = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"feature:util.u{i}");
            nodes.Add(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"  - {{key: \"{key}\", label: \"U{i}\"}}"));
            edges.Add($"  - {{from: \"code:src/util/format.ts\", to: \"{key}\", type: implements}}");
        }

        var file = Path.Combine(home, "hops-map.yaml");
        File.WriteAllText(file, string.Join('\n', ["schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:", .. nodes, "edges:", .. edges]) + "\n");
        return file;
    }

    private static Action<IServiceCollection> Wiring(string lockPath, string scenario = "") => services =>
    {
        if (string.Equals(scenario, "areas-over-budget", StringComparison.Ordinal) || string.Equals(scenario, "area-truncated", StringComparison.Ordinal))
        {
            _ = services.RemoveAll<WholeMapRules>();
            _ = services.AddSingleton(string.Equals(scenario, "area-truncated", StringComparison.Ordinal) ? new WholeMapRules(AreaCap: 5) : new WholeMapRules(NodeBudget: 5));   // the tabs map is a few more nodes than either
        }

        _ = services.RemoveAll<IClusterSource>();
        _ = services.AddSingleton(string.Equals(scenario, "building-in-progress", StringComparison.Ordinal) ? new HangingClusterSource() : (IClusterSource)new FakeClusterSource());   // a build that never ends: the answer says it is building
        _ = services.RemoveAll<ICurrentUser>();
        _ = services.AddSingleton<ICurrentUser>(new FakeCurrentUser("golden"));
        _ = services.RemoveAll<HeavyLockSettings>();
        _ = services.AddSingleton(new HeavyLockSettings(lockPath, 2, Held: false));
    };

    private static async Task Rebuild(GoldenHost host, CancellationToken ct)
    {
        await using var scope = host.App.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetServices<IScheduledService>().Single(s => s.Identity().Value == "context-rebuild");
        var grant = host.App.Services.GetRequiredService<HeldLease>().Current!;   // why: the started host holds the lease
        var built = await service.Run(new ScheduledRun(grant, DateTimeOffset.UnixEpoch), ct);
        Assert.True(built.IsSuccess, built.IsError ? built.Error.Message : string.Empty);
    }

    private sealed class Deleted(string path) : IDisposable
    {
        public void Dispose() => Directory.Delete(path, recursive: true);
    }
}
