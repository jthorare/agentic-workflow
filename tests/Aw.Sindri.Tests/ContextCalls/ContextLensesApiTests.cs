using System.Net;
using System.Text.Json;
using Aw.Context.Core.Extract;
using Aw.Context.Testing.Extract;
using Aw.Context.Testing.Fixtures;
using Aw.Context.Testing.Map;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The app's two reads (lenses and filemap), the scoping run's proposals and MCP's context_lenses (context spec §8.5, §8.9; README X63) on the fixture map.</summary>
public sealed class ContextLensesApiTests : IDisposable
{
    private const string Anchor = "feature:tabs.tab-manager";
    private readonly FixtureRepo _repo = FixtureRepo.Build();

    public void Dispose() => _repo.Dispose();

    private async Task<GoldenHost> Host(bool sessions = true)
    {
        var host = await ContextHosts.Approved(_repo, sessions, TestContext.Current.CancellationToken,
            s => { _ = s.RemoveAll<IClusterSource>(); _ = s.AddSingleton<IClusterSource>(new FakeClusterSource()); }, name: FixtureMap.Repo);
        await ContextMapApiTests.Import(host);
        return host;
    }

    private static (string Name, string Value)[] Terminal(GoldenHost host) => Human(host.Token.Value);

    private static (string Name, string Value)[] App(GoldenHost host) => [.. Human(host.Token.Value, client: "ui").Where(h => h.Name != SindriHeaders.Terminal)];

    private static (string Name, string Value)[] Session(string repo = FixtureMap.Repo) => [(SindriHeaders.Caller, "session"), (SindriHeaders.Repo, repo)];

    private static Microsoft.Extensions.Time.Testing.FakeTimeProvider Clock(GoldenHost host) =>
        (Microsoft.Extensions.Time.Testing.FakeTimeProvider)host.App.Services.GetRequiredService<TimeProvider>();

    private static List<string?> Statuses(string body) => [.. Json(body).GetProperty("surfaces").EnumerateArray().Select(s => s.GetProperty("status").GetString())];

    private static JsonElement Json(string body) => JsonDocument.Parse(body).RootElement;

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Lenses_show_the_three_bands_a_session_reads_its_scope_and_an_unknown_anchor_is_003_and_no_draft_is_044()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        var lenses = await host.Http.Ask(Get("/sindri/context/lenses?anchor=" + Uri.EscapeDataString(Anchor), Terminal(host)));
        var session = await host.Http.Ask(Get("/sindri/context/lenses?anchor=" + Uri.EscapeDataString(Anchor), Session()));
        var unknown = await host.Http.Ask(Get("/sindri/context/lenses?anchor=no-such-node", Terminal(host)));
        var draft = await host.Http.Ask(Get("/sindri/context/lenses?anchor=" + Uri.EscapeDataString(Anchor) + "&draft=true", Terminal(host)));

        Assert.Equal(HttpStatusCode.OK, lenses.Status);
        var view = Json(lenses.Body);
        Assert.Equal(Anchor, view.GetProperty("anchor").GetProperty("key").GetString());
        Assert.All(["intent", "product", "code"], name => Assert.True(view.GetProperty("bands").TryGetProperty(name, out _), name));
        Assert.All(["coverage", "conflicts", "edges", "degraded", "cites", "mapVersion", "asOf"], name => Assert.True(view.TryGetProperty(name, out _), name));
        Assert.Equal((1, 2), (view.GetProperty("schemaVersion").GetInt32(), view.GetProperty("depth").GetInt32()));
        Assert.Equal(HttpStatusCode.OK, session.Status);
        Assert.Equal((HttpStatusCode.NotFound, "SND-CONTEXT-003"), (unknown.Status, unknown.Code));
        Assert.Equal((HttpStatusCode.NotFound, "SND-CONTEXT-044"), (draft.Status, draft.Code));
    }

    private static string Lenses(params string[] pairs) => "/sindri/context/lenses?" + string.Join('&', pairs);

    private static string A(string key) => "anchor=" + Uri.EscapeDataString(key);

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Lenses_route_repeated_anchor_answers_schema_2_and_a_repeat_of_one_value_stays_schema_1()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        var two = await host.Http.Ask(Get(Lenses(A(Anchor), A("feature:layout.split-pane")), Terminal(host)));
        var repeat = await host.Http.Ask(Get(Lenses(A(Anchor), A(Anchor)), Terminal(host)));
        var partly = await host.Http.Ask(Get(Lenses(A(Anchor), A("feature:nope.x")), Terminal(host)));

        Assert.Equal(HttpStatusCode.OK, two.Status);
        Assert.Equal((2, 2), (Json(two.Body).GetProperty("schemaVersion").GetInt32(), Json(two.Body).GetProperty("anchors").GetArrayLength()));
        Assert.Equal(1, Json(repeat.Body).GetProperty("schemaVersion").GetInt32());
        Assert.Equal(["feature:nope.x"], Json(partly.Body).GetProperty("missingAnchors").EnumerateArray().Select(e => e.GetString()), StringComparer.Ordinal);
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Lenses_route_takes_fifty_anchors_and_a_draft_with_a_map_version_is_006()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        var many = await host.Http.Ask(Get(Lenses([.. Enumerable.Range(0, 50).Select(i => A("feature:nope." + i.ToString(System.Globalization.CultureInfo.InvariantCulture)))]), Terminal(host)));
        var both = await host.Http.Ask(Get(Lenses(A(Anchor), "draft=true", "mapVersion=v1"), Terminal(host)));
        var malformed = await host.Http.Ask(Get(Lenses(A(Anchor), "mapVersion=latest"), Terminal(host)));

        Assert.Equal((HttpStatusCode.NotFound, "SND-CONTEXT-003"), (many.Status, many.Code));
        Assert.Equal((HttpStatusCode.BadRequest, "SND-CONTEXT-006"), (both.Status, both.Code));
        Assert.Equal((HttpStatusCode.BadRequest, "SND-CONTEXT-006"), (malformed.Status, malformed.Code));
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Lenses_route_mapVersion_unretained_is_120_problem_404_and_a_held_version_reads_as_v_N()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        var gone = await host.Http.Ask(Get(Lenses(A(Anchor), "mapVersion=v99"), Terminal(host)));
        var goneSet = await host.Http.Ask(Get(Lenses(A(Anchor), A("feature:layout.split-pane"), "mapVersion=v99"), Terminal(host)));
        var held = await host.Http.Ask(Get(Lenses(A(Anchor), "mapVersion=v1"), Terminal(host)));

        Assert.Equal((HttpStatusCode.NotFound, "SND-CONTEXT-120"), (gone.Status, gone.Code));
        Assert.Equal((HttpStatusCode.NotFound, "SND-CONTEXT-120"), (goneSet.Status, goneSet.Code));
        Assert.Equal("v1", Json(held.Body).GetProperty("mapVersion").GetString());
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Lenses_route_one_anchor_keeps_schema_1_and_adds_searched_and_map_coverage()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        var one = Json((await host.Http.Ask(Get(Lenses(A(Anchor)), Terminal(host)))).Body);

        Assert.Equal(1, one.GetProperty("schemaVersion").GetInt32());
        Assert.True(one.TryGetProperty("anchor", out _));
        Assert.False(one.TryGetProperty("anchors", out _));
        Assert.True(one.GetProperty("searched").TryGetProperty("sources", out _));
        Assert.True(one.GetProperty("coverage").TryGetProperty("map", out _));
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Lenses_and_query_routes_take_hops_echo_it_and_refuse_an_unknown_name_with_140()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        var plain = await host.Http.Ask(Get(Lenses(A(Anchor)), Terminal(host)));
        var hops = await host.Http.Ask(Get(Lenses(A(Anchor), "hops=state,dependency,flow,component"), Terminal(host)));
        var set = await host.Http.Ask(Get(Lenses(A(Anchor), A("feature:layout.split-pane"), "hops=component"), Terminal(host)));
        var bogus = await host.Http.Ask(Get(Lenses(A(Anchor), "hops=bogus"), Terminal(host)));
        var query = await host.Http.Ask(Get("/sindri/context/query?node=" + Uri.EscapeDataString(Anchor) + "&hops=bogus", Terminal(host)));
        var queryOk = await host.Http.Ask(Get("/sindri/context/query?node=" + Uri.EscapeDataString(Anchor) + "&hops=component", Terminal(host)));

        Assert.Equal(["state", "dependency", "flow"], Json(plain.Body).GetProperty("searched").GetProperty("hops").EnumerateArray().Select(e => e.GetString()), StringComparer.Ordinal);
        Assert.Equal(["state", "dependency", "flow", "component"], Json(hops.Body).GetProperty("searched").GetProperty("hops").EnumerateArray().Select(e => e.GetString()), StringComparer.Ordinal);
        Assert.Equal(HttpStatusCode.OK, set.Status);
        Assert.Equal((HttpStatusCode.BadRequest, "SND-CONTEXT-140"), (bogus.Status, bogus.Code));
        Assert.Equal((HttpStatusCode.BadRequest, "SND-CONTEXT-140"), (query.Status, query.Code));
        Assert.Equal(HttpStatusCode.OK, queryOk.Status);
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Context_lenses_and_query_over_mcp_take_hops_and_refuse_an_unknown_name_with_140()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Host();
        await using var mcp = await McpSession.Open(host, FixtureMap.Repo, ct);

        var tools = await mcp.Client.ListToolsAsync(cancellationToken: ct);
        var ok = await mcp.Call("context_lenses", $$"""{"anchor":"{{Anchor}}","hops":"state,component"}""", ct);
        var bad = await mcp.Call("context_lenses", $$"""{"anchor":"{{Anchor}}","hops":"bogus"}""", ct);
        var queryOk = await mcp.Call("context_query", $$"""{"node":"{{Anchor}}","hops":"component"}""", ct);
        var queryBad = await mcp.Call("context_query", $$"""{"node":"{{Anchor}}","hops":"bogus"}""", ct);

        foreach (var name in (string[])["context_lenses", "context_query"])
        {
            Assert.Equal("string", tools.Single(t => t.Name == name).JsonSchema.GetProperty("properties").GetProperty("hops").GetProperty("type").GetString());
        }

        Assert.NotEqual(true, ok.IsError);
        Assert.Equal(["state", "component"], ok.StructuredContent!.Value.GetProperty("searched").GetProperty("hops").EnumerateArray().Select(e => e.GetString()), StringComparer.Ordinal);
        Assert.Contains("SND-CONTEXT-140", bad.StructuredContent!.Value.GetRawText(), StringComparison.Ordinal);
        Assert.NotEqual(true, queryOk.IsError);
        Assert.Contains("SND-CONTEXT-140", queryBad.StructuredContent!.Value.GetRawText(), StringComparison.Ordinal);
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Filemap_lists_direct_children_with_rollups_and_a_repo_outside_the_profile_or_a_session_scope_gives_the_same_045()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        var root = await host.Http.Ask(Get($"/sindri/context/filemap?repo={FixtureMap.Repo}&path=src/", Terminal(host)));
        var unmapped = await host.Http.Ask(Get($"/sindri/context/filemap?repo={FixtureMap.Repo}&path=src/&unmapped=true", Terminal(host)));
        var nope = await host.Http.Ask(Get("/sindri/context/filemap?repo=nope", Terminal(host)));
        var outside = await host.Http.Ask(Get($"/sindri/context/filemap?repo={FixtureMap.Repo}", Session("other")));
        var draft = await host.Http.Ask(Get($"/sindri/context/filemap?repo={FixtureMap.Repo}&draft=true", Terminal(host)));

        Assert.Equal(HttpStatusCode.OK, root.Status);
        var view = Json(root.Body);
        Assert.Equal("src/", view.GetProperty("path").GetString());
        Assert.Contains(view.GetProperty("entries").EnumerateArray(), e => e.GetProperty("name").GetString() == "layout" && e.GetProperty("type").GetString() == "dir");
        Assert.Equal(1, view.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(HttpStatusCode.OK, unmapped.Status);
        Assert.Equal((HttpStatusCode.NotFound, "SND-CONTEXT-045"), (nope.Status, nope.Code));
        Assert.Equal((HttpStatusCode.NotFound, "SND-CONTEXT-045"), (outside.Status, outside.Code));
        Assert.Equal(Json(nope.Body).GetProperty("status").GetInt32(), Json(outside.Body).GetProperty("status").GetInt32());
        Assert.Equal((HttpStatusCode.NotFound, "SND-CONTEXT-044"), (draft.Status, draft.Code));
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Context_lenses_over_mcp_returns_the_lenses_as_structured_content_and_a_session_off_is_020()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Host();
        await using var mcp = await McpSession.Open(host, FixtureMap.Repo, ct);

        var tools = await mcp.Client.ListToolsAsync(cancellationToken: ct);
        var result = await mcp.Call("context_lenses", $$"""{"anchor":"{{Anchor}}"}""", ct);
        var bad = await mcp.Call("context_lenses", """{"anchor":"x","draft":true}""", ct);

        Assert.Contains("context_lenses", tools.Select(t => t.Name), StringComparer.Ordinal);
        Assert.True(tools.Single(t => t.Name == "context_lenses").ProtocolTool.Annotations?.ReadOnlyHint);
        Assert.NotEqual(true, result.IsError);
        Assert.Equal(Anchor, result.StructuredContent!.Value.GetProperty("anchor").GetProperty("key").GetString());
        Assert.Equal(true, bad.IsError);   // no draft argument over MCP
        await using var off = await OffHost();
        await using var offMcp = await McpSession.Open(off, FixtureMap.Repo, ct);
        var refused = await offMcp.Call("context_lenses", $$"""{"anchor":"{{Anchor}}"}""", ct);
        Assert.Equal(true, refused.IsError);
        Assert.Contains("SND-CONTEXT-020", refused.StructuredContent!.Value.GetRawText(), StringComparison.Ordinal);
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Context_lenses_over_mcp_fences_the_untrusted_labels_the_way_a_session_over_http_reads_them()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Host();
        await ImportDrafted(host);
        await using var mcp = await McpSession.Open(host, FixtureMap.Repo, ct);

        var result = await mcp.Call("context_lenses", """{"anchor":"feature:a"}""", ct);

        Assert.NotEqual(true, result.IsError);
        var own = result.StructuredContent!.Value.GetProperty("bands").GetProperty("product").GetProperty("own").EnumerateArray().Single(i => i.GetProperty("node").GetString() == "feature:b");
        Assert.Equal(("<untrusted source=\"map\">Drafted B</untrusted>", "untrusted"), (own.GetProperty("label").GetString(), own.GetProperty("trust").GetString()));
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Context_lenses_over_mcp_takes_optional_anchors_and_mapVersion_and_a_lone_anchor_stays_schema_1()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Host();
        await using var mcp = await McpSession.Open(host, FixtureMap.Repo, ct);

        var tool = (await mcp.Client.ListToolsAsync(cancellationToken: ct)).Single(t => t.Name == "context_lenses").JsonSchema;
        var lone = await mcp.Call("context_lenses", $$"""{"anchor":"{{Anchor}}"}""", ct);
        var set = await mcp.Call("context_lenses", $$"""{"anchors":["{{Anchor}}","feature:layout.split-pane","feature:nope.x"]}""", ct);
        var held = await mcp.Call("context_lenses", $$"""{"anchor":"{{Anchor}}","mapVersion":"v1"}""", ct);
        var gone = await mcp.Call("context_lenses", $$"""{"anchor":"{{Anchor}}","mapVersion":"v99"}""", ct);
        var goneSet = await mcp.Call("context_lenses", $$"""{"anchors":["{{Anchor}}","feature:layout.split-pane"],"mapVersion":"v99"}""", ct);
        var none = await mcp.Call("context_lenses", "{}", ct);
        var wrongType = await mcp.Call("context_lenses", """{"anchors":"feature:a"}""", ct);
        var wrongItem = await mcp.Call("context_lenses", """{"anchors":[1]}""", ct);

        Assert.Equal(["anchor", "anchors", "mapVersion", "hops"], tool.GetProperty("properties").EnumerateObject().Select(p => p.Name), StringComparer.Ordinal);
        Assert.False(tool.TryGetProperty("required", out _));
        Assert.Equal((1, Anchor), (lone.StructuredContent!.Value.GetProperty("schemaVersion").GetInt32(), lone.StructuredContent!.Value.GetProperty("anchor").GetProperty("key").GetString()));
        Assert.Equal((2, 2), (set.StructuredContent!.Value.GetProperty("schemaVersion").GetInt32(), set.StructuredContent!.Value.GetProperty("anchors").GetArrayLength()));
        Assert.Equal(["feature:nope.x"], set.StructuredContent!.Value.GetProperty("missingAnchors").EnumerateArray().Select(e => e.GetString()), StringComparer.Ordinal);
        Assert.Equal("v1", held.StructuredContent!.Value.GetProperty("mapVersion").GetString());
        Assert.All([gone, goneSet], r => { Assert.Equal(true, r.IsError); Assert.Contains("SND-CONTEXT-120", r.StructuredContent!.Value.GetRawText(), StringComparison.Ordinal); });
        Assert.All([none, wrongType, wrongItem], r => { Assert.Equal(true, r.IsError); Assert.Contains("SND-CONTEXT-022", r.StructuredContent!.Value.GetRawText(), StringComparison.Ordinal); });
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Context_query_over_mcp_takes_an_optional_mapVersion()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Host();
        await using var mcp = await McpSession.Open(host, FixtureMap.Repo, ct);

        var schema = (await mcp.Client.ListToolsAsync(cancellationToken: ct)).Single(t => t.Name == "context_query").JsonSchema;
        var gone = await mcp.Call("context_query", $$"""{"node":"{{Anchor}}","mapVersion":"v99"}""", ct);
        var malformed = await mcp.Call("context_query", $$"""{"node":"{{Anchor}}","mapVersion":"latest"}""", ct);

        Assert.True(schema.GetProperty("properties").TryGetProperty("mapVersion", out _));
        Assert.Contains("SND-CONTEXT-120", gone.StructuredContent!.Value.GetRawText(), StringComparison.Ordinal);
        Assert.Contains("SND-CONTEXT-006", malformed.StructuredContent!.Value.GetRawText(), StringComparison.Ordinal);
    }

    private async Task<GoldenHost> OffHost() => await Host(sessions: false);

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Lenses_depth_is_0_or_more_deeper_than_8_is_read_as_8_and_negative_is_006_over_http()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();
        var route = "/sindri/context/lenses?anchor=" + Uri.EscapeDataString(Anchor);

        var zero = await host.Http.Ask(Get(route + "&depth=0", Terminal(host)));
        var four = await host.Http.Ask(Get(route + "&depth=4", Terminal(host)));
        var five = await host.Http.Ask(Get(route + "&depth=5", Terminal(host)));
        var deep = await host.Http.Ask(Get(route + "&depth=99", Terminal(host)));
        var negative = await host.Http.Ask(Get(route + "&depth=-1", Terminal(host)));

        Assert.Equal((HttpStatusCode.OK, 0), (zero.Status, Json(zero.Body).GetProperty("depth").GetInt32()));
        Assert.Equal((HttpStatusCode.OK, 4), (four.Status, Json(four.Body).GetProperty("depth").GetInt32()));
        Assert.Equal((HttpStatusCode.OK, 5), (five.Status, Json(five.Body).GetProperty("depth").GetInt32()));   // the old cap of 4 was raised to 8 (owner q93, 2026-10-23)
        Assert.Equal(HttpStatusCode.OK, deep.Status);
        Assert.Equal((HttpStatusCode.BadRequest, "SND-CONTEXT-006"), (negative.Status, negative.Code));
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task An_app_caller_reads_a_clean_label_with_trust_untrusted()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();
        await ImportDrafted(host);

        var app = await host.Http.Ask(Get("/sindri/context/lenses?anchor=feature%3Aa", App(host)));
        var terminal = await host.Http.Ask(Get("/sindri/context/lenses?anchor=feature%3Aa", Terminal(host)));

        Assert.Equal(("Drafted B", "untrusted"), DraftedLabel(app.Body));
        Assert.Equal(("Drafted B", "untrusted"), DraftedLabel(terminal.Body));
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task A_session_caller_reads_the_fenced_label()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();
        await ImportDrafted(host);

        var session = await host.Http.Ask(Get("/sindri/context/lenses?anchor=feature%3Aa", Session()));

        Assert.Equal(("<untrusted source=\"map\">Drafted B</untrusted>", "untrusted"), DraftedLabel(session.Body));
    }

    private static (string Label, string Trust) DraftedLabel(string body)
    {
        var item = Json(body).GetProperty("bands").GetProperty("product").GetProperty("own").EnumerateArray().Single(i => i.GetProperty("node").GetString() == "feature:b");
        return (item.GetProperty("label").GetString()!, item.GetProperty("trust").GetString()!);
    }

    /// <summary>A map whose feature B is a model's draft, with a deterministic file that implements both in the fixture repo (so a session of that repo sees them).</summary>
    private static async Task ImportDrafted(GoldenHost host)
    {
        var file = Path.Combine(host.Home, "drafted-map.yaml");
        await File.WriteAllTextAsync(file, string.Join('\n',
            "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
            "  - {key: \"feature:a\", label: \"A\"}",
            "  - {key: \"feature:b\", label: \"Drafted B\", provenance: model, confidence: 0.5}",
            "  - {key: \"code:src/a.ts\", label: \"a.ts\", provenance: deterministic}",
            "edges:",
            "  - {from: \"feature:b\", to: \"feature:a\", type: part-of}",
            "  - {from: \"code:src/a.ts\", to: \"feature:a\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/a.ts\"}]}",
            "  - {from: \"code:src/a.ts\", to: \"feature:b\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/a.ts\"}]}") + "\n", TestContext.Current.CancellationToken);
        await ContextMapApiTests.Import(host, file);
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task A_scoping_run_proposes_into_a_draft_idempotently_never_again_after_a_reject_and_never_into_current()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Host();
        var body = $$"""{"run":"scope-1","repo":"{{FixtureMap.Repo}}","surfaces":[{"name":"Tab manager","key":"{{Anchor}}"},{"name":"Zoom","kind":"feature","evidence":[]},{"name":"Something vague"}]}""";

        var first = await host.Http.Ask(Post("/sindri/context/map/proposals", body, App(host)));
        var second = await host.Http.Ask(Post("/sindri/context/map/proposals", body, App(host)));
        var session = await host.Http.Ask(Post("/sindri/context/map/proposals", body, Session()));
        var view = await host.Http.Ask(Get("/sindri/context/map/proposals?run=scope-1", App(host)));
        Clock(host).Advance(TimeSpan.FromMinutes(1));   // why: the golden host's clock stands still, and a decision is ordered after what it decides about by its time
        var reject = await host.Http.Ask(Post("/sindri/context/map/review", """{"verb":"reject","node":"feature:zoom"}""", App(host)));
        var third = await host.Http.Ask(Post("/sindri/context/map/proposals", body, App(host)));
        var status = await host.Http.Ask(Get("/sindri/context/map", Terminal(host)));
        var recorded = await host.App.Services.GetRequiredService<ILedgerStore>().Latest(EventTypes.ContextMapProposed, ct);

        Assert.True(first.Status == HttpStatusCode.OK, first.Body);
        Assert.Equal(1, Json(first.Body).GetProperty("result").GetProperty("added").GetInt32());
        Assert.Equal(["mapped", "new", "unmapped"], Statuses(first.Body), StringComparer.Ordinal);   // the response carries each surface's standing too (spec §8.7), not the counts alone
        Assert.Equal(Anchor, Json(first.Body).GetProperty("surfaces")[0].GetProperty("node").GetString());
        Assert.Equal("Tab manager", Json(first.Body).GetProperty("surfaces")[0].GetProperty("surface").GetString());
        Assert.Equal(["mapped", "new", "unmapped"], Statuses(second.Body), StringComparer.Ordinal);   // an idempotent re-run reports the same standing
        Assert.Equal(0, Json(second.Body).GetProperty("result").GetProperty("added").GetInt32());
        Assert.Equal((HttpStatusCode.Forbidden, "SND-AUTH-002"), (session.Status, session.Code));
        Assert.Equal(["mapped", "new", "unmapped"], Statuses(view.Body), StringComparer.Ordinal);   // every surface of the run, in the order it was submitted: the mapped one and the one with no kind wrote no row
        Assert.Equal(HttpStatusCode.OK, reject.Status);
        Assert.Equal(["mapped", "unmapped", "unmapped"], Statuses((await host.Http.Ask(Get("/sindri/context/map/proposals?run=scope-1", App(host)))).Body), StringComparer.Ordinal);
        Assert.Equal(0, Json(third.Body).GetProperty("result").GetProperty("added").GetInt32());
        Assert.Equal(1, Json(third.Body).GetProperty("result").GetProperty("rejected").GetInt32());
        Assert.Contains("\"status\":\"current\"", status.Body, StringComparison.Ordinal);
        Assert.Contains("scope-1", recorded.Value!.Envelope.Payload, StringComparison.Ordinal);
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task A_proposal_without_the_lease_is_refused_before_any_row_is_written_and_the_event_key_is_the_run_and_a_hash_of_its_rows()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Host();
        var body = $$"""{"run":"scope-2","repo":"{{FixtureMap.Repo}}","surfaces":[{"name":"Zoom","kind":"feature"}]}""";
        var held = host.App.Services.GetRequiredService<HeldLease>();
        var grant = held.Current!;
        held.Clear();

        var refused = await host.Http.Ask(Post("/sindri/context/map/proposals", body, App(host)));
        held.Hold(grant);
        var view = await host.Http.Ask(Get("/sindri/context/map/proposals?run=scope-2", App(host)));
        var accepted = await host.Http.Ask(Post("/sindri/context/map/proposals", body, App(host)));
        var recorded = await host.App.Services.GetRequiredService<ILedgerStore>().Latest(EventTypes.ContextMapProposed, ct);

        Assert.Equal((HttpStatusCode.ServiceUnavailable, "SND-LEDGER-002"), (refused.Status, refused.Code));   // the command gate needs the lease to record the call, so it answers before the service does
        Assert.Empty(Json(view.Body).GetProperty("surfaces").EnumerateArray());   // no row and no run list were written
        Assert.Equal(1, Json(accepted.Body).GetProperty("result").GetProperty("added").GetInt32());   // so the retry adds the row and records the event
        Assert.Matches(@"^context\.map\.proposed:scope-2:[0-9a-f]{16}$", recorded.Value!.IdempotencyKey);
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Two_proposals_of_one_run_with_different_surfaces_each_record_an_event_and_a_repeat_records_none()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();
        static string Body(string name) => $$"""{"run":"scope-3","repo":"{{FixtureMap.Repo}}","surfaces":[{"name":"{{name}}","kind":"feature"}]}""";

        var one = await host.Http.Ask(Post("/sindri/context/map/proposals", Body("Zoom"), App(host)));
        var two = await host.Http.Ask(Post("/sindri/context/map/proposals", Body("Pan"), App(host)));
        var repeat = await host.Http.Ask(Post("/sindri/context/map/proposals", Body("Pan"), App(host)));

        var sequences = new[] { one, two, repeat }.Select(r => Json(r.Body).GetProperty("sequence")).ToList();
        Assert.Equal(JsonValueKind.Number, sequences[0].ValueKind);
        Assert.Equal(JsonValueKind.Number, sequences[1].ValueKind);   // the second call added one row, so it has its own event even though its counts equal the first call's
        Assert.NotEqual(sequences[0].GetInt64(), sequences[1].GetInt64());
        Assert.Equal(JsonValueKind.Null, sequences[2].ValueKind);
        var back = await host.Http.Ask(Post("/sindri/context/map/proposals", Body("Zoom"), App(host)));   // the first set of surfaces again: its event is in the ledger
        Assert.Equal(JsonValueKind.Null, Json(back.Body).GetProperty("sequence").ValueKind);
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task A_retry_after_the_event_append_failed_records_the_event_the_committed_rows_lack()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        var ledger = new FlakyLedger();
        await using var host = await ContextHosts.Approved(_repo, sessions: true, TestContext.Current.CancellationToken,
            s => { _ = s.RemoveAll<IClusterSource>(); _ = s.AddSingleton<IClusterSource>(new FakeClusterSource()); ledger.Wrap(s); }, name: FixtureMap.Repo);
        await ContextMapApiTests.Import(host);
        var body = $$"""{"run":"scope-4","repo":"{{FixtureMap.Repo}}","surfaces":[{"name":"Zoom","kind":"feature"}]}""";

        ledger.Failing = true;
        var failed = await host.Http.Ask(Post("/sindri/context/map/proposals", body, App(host)));
        ledger.Failing = false;
        var retry = await host.Http.Ask(Post("/sindri/context/map/proposals", body, App(host)));
        var again = await host.Http.Ask(Post("/sindri/context/map/proposals", body, App(host)));

        Assert.Equal((HttpStatusCode.ServiceUnavailable, "SND-LEDGER-002"), (failed.Status, failed.Code));
        Assert.Equal(0, Json(retry.Body).GetProperty("result").GetProperty("added").GetInt32());   // the row committed the first time
        Assert.Equal(JsonValueKind.Number, Json(retry.Body).GetProperty("sequence").ValueKind);   // and the retry wrote its event
        Assert.Equal(JsonValueKind.Null, Json(again.Body).GetProperty("sequence").ValueKind);
        var recorded = await host.App.Services.GetRequiredService<ILedgerStore>().Latest(EventTypes.ContextMapProposed, TestContext.Current.CancellationToken);
        var payload = Json(recorded.Value!.Envelope.Payload);
        Assert.Equal((1, 0), (payload.GetProperty("added").GetInt32(), payload.GetProperty("existing").GetInt32()));   // the re-emitted event says what the committed call added, not the retry's 0
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task A_retry_after_the_withdraw_event_append_failed_records_the_event_exactly_once()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        var ledger = new FlakyLedger();
        await using var host = await ContextHosts.Approved(_repo, sessions: true, TestContext.Current.CancellationToken,
            s => { _ = s.RemoveAll<IClusterSource>(); _ = s.AddSingleton<IClusterSource>(new FakeClusterSource()); ledger.Wrap(s); }, name: FixtureMap.Repo);
        await ContextMapApiTests.Import(host);
        var body = $$"""{"run":"scope-5","repo":"{{FixtureMap.Repo}}","surfaces":[{"name":"Zoom","kind":"feature"}]}""";
        var empty = $$"""{"run":"scope-5","repo":"{{FixtureMap.Repo}}","surfaces":[]}""";
        Assert.Equal(HttpStatusCode.OK, (await host.Http.Ask(Post("/sindri/context/map/proposals", body, App(host)))).Status);

        ledger.Failing = true;
        var failed = await host.Http.Ask(Post("/sindri/context/map/proposals", empty, App(host)));
        ledger.Failing = false;
        var retry = await host.Http.Ask(Post("/sindri/context/map/proposals", empty, App(host)));
        var again = await host.Http.Ask(Post("/sindri/context/map/proposals", empty, App(host)));

        Assert.Equal((HttpStatusCode.ServiceUnavailable, "SND-LEDGER-002"), (failed.Status, failed.Code));
        Assert.Equal(JsonValueKind.Number, Json(retry.Body).GetProperty("sequence").ValueKind);   // the rows committed the first time; the retry writes the event they lack
        Assert.Equal(JsonValueKind.Null, Json(again.Body).GetProperty("sequence").ValueKind);   // and only once
        var recorded = await host.App.Services.GetRequiredService<ILedgerStore>().Latest(EventTypes.ContextMapProposed, TestContext.Current.CancellationToken);
        Assert.Equal(1, Json(recorded.Value!.Envelope.Payload).GetProperty("withdrawn").GetInt32());
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Each_withdraw_event_counts_its_own_withdrawal_across_withdraw_repropose_withdraw()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ContextHosts.Approved(_repo, sessions: true, ct,
            s => { _ = s.RemoveAll<IClusterSource>(); _ = s.AddSingleton<IClusterSource>(new FakeClusterSource()); }, name: FixtureMap.Repo);
        await ContextMapApiTests.Import(host);
        var body = $$"""{"run":"scope-6","repo":"{{FixtureMap.Repo}}","surfaces":[{"name":"Zoom","kind":"feature"},{"name":"Swap","kind":"feature"}]}""";
        var empty = $$"""{"run":"scope-6","repo":"{{FixtureMap.Repo}}","surfaces":[]}""";

        _ = await host.Http.Ask(Post("/sindri/context/map/proposals", body, App(host)));
        var first = await host.Http.Ask(Post("/sindri/context/map/proposals", empty, App(host)));
        _ = await host.Http.Ask(Post("/sindri/context/map/proposals", body, App(host)));
        var second = await host.Http.Ask(Post("/sindri/context/map/proposals", empty, App(host)));

        var events = (await host.App.Services.GetRequiredService<ILedgerStore>().ReadAfter(0, 500, ct)).Value.Where(e => e.Envelope.Type == EventTypes.ContextMapProposed)
            .Select(e => Json(e.Envelope.Payload)).Where(p => p.TryGetProperty("withdrawn", out _)).Select(p => p.GetProperty("withdrawn").GetInt32()).ToList();
        Assert.Equal((2, 2), (Json(first.Body).GetProperty("result").GetProperty("withdrawn").GetInt32(), Json(second.Body).GetProperty("result").GetProperty("withdrawn").GetInt32()));
        Assert.Equal([2, 2], events);   // each event says what its own call withdrew, not the run's running total
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task A_draft_opened_from_a_map_imported_without_a_commit_promotes_instead_of_failing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Host();
        var body = $$"""{"run":"scope-1","repo":"{{FixtureMap.Repo}}","surfaces":[{"name":"Tab manager","key":"{{Anchor}}"}]}""";
        Assert.Equal(HttpStatusCode.OK, (await host.Http.Ask(Post("/sindri/context/map/proposals", body, App(host)))).Status);

        var preview = await host.Http.Ask(Post("/sindri/context/map/promote", "{}", Terminal(host)));
        var shown = Json(preview.Body);
        var draft = shown.GetProperty("draft").GetString();
        var challenge = shown.GetProperty("challenge");
        var answer = new { challenge = challenge.GetProperty("challenge").GetString(), typed = challenge.GetProperty("action").GetString() };
        var promoted = await host.Http.Ask(Post("/sindri/context/map/promote", JsonSerializer.Serialize(new { draft, answer }), Terminal(host)));
        var recorded = await host.App.Services.GetRequiredService<ILedgerStore>().Latest(EventTypes.ContextMapPromoted, ct);

        Assert.True(preview.Status == HttpStatusCode.OK, preview.Body);
        Assert.DoesNotContain('@', draft!);   // why: an imported map has no commit, so its draft's label is a bare 'v<N>'
        Assert.True(promoted.Status == HttpStatusCode.OK, promoted.Body);
        Assert.NotNull(recorded.Value);
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task EvalMap_route_answers_numbers_and_refuses_sessions_020()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        var human = await host.Http.Ask(Get("/sindri/context/eval/map", Terminal(host)));
        var session = await host.Http.Ask(Get("/sindri/context/eval/map", Session()));

        Assert.Equal(HttpStatusCode.OK, human.Status);
        var run = Json(human.Body);
        Assert.Equal(1, run.GetProperty("report").GetProperty("schemaVersion").GetInt32());
        Assert.True(run.GetProperty("report").TryGetProperty("nodePrecision", out _));
        Assert.StartsWith("map", Path.GetFileName(run.GetProperty("path").GetString()), StringComparison.Ordinal);
        Assert.Contains(Path.Combine("eval", "map"), run.GetProperty("path").GetString(), StringComparison.Ordinal);
        Assert.Equal("SND-CONTEXT-020", session.Code);
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task ContextPurge_service_applies_eval_ttl()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();
        var made = Json((await host.Http.Ask(Get("/sindri/context/eval/map", Terminal(host)))).Body).GetProperty("path").GetString()!;
        var fresh = made.Replace("map-", "map-fresh-", StringComparison.Ordinal);
        File.Copy(made, fresh);
        var now = Clock(host).GetUtcNow().UtcDateTime;   // the host runs on a fake clock
        File.SetLastWriteTimeUtc(made, now.AddDays(-100));
        File.SetLastWriteTimeUtc(fresh, now);
        await using var scope = host.App.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetServices<Core.Scheduling.IScheduledService>().Single(s => s.Identity().Value == "context-purge");
        var grant = host.App.Services.GetRequiredService<HeldLease>().Current!;   // why: the started host holds the lease

        var ran = await service.Run(new Core.Scheduling.ScheduledRun(grant, Clock(host).GetUtcNow()), TestContext.Current.CancellationToken);

        Assert.True(ran.IsSuccess);
        Assert.False(File.Exists(made));
        Assert.True(File.Exists(fresh));
    }
}
