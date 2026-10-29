using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>The context routes over the canned handler (context spec §8): what the app sends, and each refusal as Sindri wrote it.</summary>
[Lane(TestLane.Unit)]
public sealed class SindriApiClientContextTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static (SindriApiClient Client, FakeSindriApi Server, FakeSindriHandler Handler) Reach()
    {
        var server = new FakeSindriApi();
        var handler = new FakeSindriHandler(server);
        return (new SindriApiClient(new HttpClient(handler), new InMemoryHumanToken(A.HumanToken), TimeProvider.System), server, handler);
    }

    private static string[] Anchors(string scenario) => [.. GoldenFiles.Node("context-lenses", scenario)["anchors"]!.AsArray().Select(a => (string)a!["key"]!)];

    private static string Query(FakeSindriHandler handler) => Uri.UnescapeDataString(Assert.Single(handler.Requests).Query);

    [Fact]
    public async Task Lenses_sends_the_anchors_and_flags_as_the_body_of_a_QUERY()
    {
        var (client, server, handler) = Reach();
        var anchors = Anchors("set-two-anchors");
        LensesQuery? served = null;
        server.LensesAnswer = q => { served = q; return ContextAnswers.Lenses(q); };

        _ = await client.Lenses(_api, new LensesQuery(anchors, Depth: 1, Draft: true, MapVersion: "v1"), Token);

        var seen = Assert.Single(handler.Requests);
        Assert.Equal(("QUERY", "/sindri/context/lenses", string.Empty), (seen.Method, seen.Path, seen.Query));
        Assert.Equal(anchors, served!.Anchors);
        Assert.Equal((1, true, "v1"), (served.Depth, served.Draft, served.MapVersion));
    }

    [Fact]
    public async Task Lenses_sends_five_hundred_anchors_in_one_read_with_nothing_in_the_url()
    {
        var (client, server, handler) = Reach();
        var many = Enumerable.Range(0, 500).Select(i => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"feature:a.{i}")).ToList();
        var count = 0;
        server.LensesAnswer = q => { count = q.Anchors.Count; return ContextAnswers.Lenses(q); };

        _ = await client.Lenses(_api, new LensesQuery(many, Depth: null, Draft: false, MapVersion: null), Token);

        Assert.Equal(500, count);
        Assert.Equal(string.Empty, Assert.Single(handler.Requests).Query);
    }

    // Keys hold ':', '/' and '#': a node's travels as an escaped query value, a lens' anchors in the body; neither becomes a path segment.
    [Fact]
    public async Task Keys_travel_in_the_query_or_the_body_never_the_path()
    {
        var (client, server, handler) = Reach();
        var key = (string)GoldenFiles.Node("context-node", "tab-manager")["node"]!["key"]! + "/x#y";
        IReadOnlyList<string>? sent = null;
        server.LensesAnswer = q => { sent = q.Anchors; return ContextAnswers.Lenses(q); };

        _ = await client.Node(_api, key, Token);
        _ = await client.Lenses(_api, new LensesQuery([key], Depth: null, Draft: false, MapVersion: null), Token);

        Assert.Equal(["/sindri/context/node", "/sindri/context/lenses"], handler.Requests.Select(r => r.Path), StringComparer.Ordinal);
        Assert.Contains(Uri.EscapeDataString(key), handler.Requests[0].Query, StringComparison.Ordinal);
        Assert.Equal(string.Empty, handler.Requests[1].Query);
        Assert.Equal([key], sent);
    }

    [Theory]
    [InlineData("unknown-anchor-003", "SND-CONTEXT-003", ErrorKind.NotFound, "suggestedAnchors")]
    [InlineData("ambiguous-037", "SND-CONTEXT-037", ErrorKind.Conflict, "candidates")]
    [InlineData("no-draft-044", "SND-CONTEXT-044", null, null)]
    [InlineData("bad-depth-006", "SND-CONTEXT-006", null, null)]
    [InlineData("query-no-anchor-006", "SND-CONTEXT-006", null, null)]
    [InlineData("map-version-not-retained-120", "SND-CONTEXT-120", null, null)]
    public async Task Lenses_maps_each_refusal_to_its_code_detail_and_extension(string scenario, string code, ErrorKind? kind, string? extension)
    {
        var (client, server, _) = Reach();
        var golden = RouteAnswer.Problem("context-lenses", scenario);
        server.LensesAnswer = _ => golden;

        var lenses = await client.Lenses(_api, new LensesQuery(["feature:x"], Depth: null, Draft: false, MapVersion: null), Token);

        var served = JsonNode.Parse(golden.Body)!;
        Assert.Equal((code, (string)served["detail"]!), (lenses.Error.Code, lenses.Error.Message));
        if (kind is { } expectedKind)
        {
            Assert.Equal(expectedKind, lenses.Error.Kind);
        }

        if (extension is not null)
        {
            var values = lenses.Error.Extensions!.Single(e => e.Key == extension).Value.EnumerateArray().Select(v => v.GetString());
            Assert.Equal(served[extension]!.AsArray().Select(v => (string?)v), values, StringComparer.Ordinal);
        }
    }

    [Fact]
    public async Task FileMap_sends_repo_path_unmapped_and_draft()
    {
        var (client, _, handler) = Reach();
        var served = GoldenFiles.Node("context-filemap", "src-unmapped");

        var map = await client.FileMap(_api, new FileMapQuery((string)served["repo"]!, Path: (string)served["path"]!, UnmappedOnly: true, Draft: true), Token);

        _ = map;
        Assert.Equal($"?repo={(string)served["repo"]!}&path={(string)served["path"]!}&unmapped=true&draft=true", Query(handler));
    }

    [Fact]
    public async Task Code_sends_the_anchors_the_draft_flag_and_the_map_version_as_a_QUERY_body()
    {
        var (client, server, handler) = Reach();
        var key = (string)GoldenFiles.Node("context-code", "edges-and-lexical")["anchors"]![0]!["key"]!;

        _ = await client.Code(_api, new CodeQuery([key, "feature:other"], Draft: true, MapVersion: "v2"), Token);

        var seen = Assert.Single(handler.Requests);
        Assert.Equal(("QUERY", "/sindri/context/code", string.Empty), (seen.Method, seen.Path, seen.Query));
        var served = Assert.Single(server.CodeQueries);
        Assert.Equal([key, "feature:other"], served.Anchors);
        Assert.Equal((true, "v2"), (served.Draft, served.MapVersion));
    }

    [Fact]
    public async Task Code_maps_a_call_with_no_anchor_to_006_and_its_detail()
    {
        var (client, server, _) = Reach();
        var golden = RouteAnswer.Problem("context-code", "query-no-anchor-006");
        server.CodeAnswer = golden;

        var code = await client.Code(_api, new CodeQuery([], Draft: false, MapVersion: null), Token);

        Assert.Equal(("SND-CONTEXT-006", (string)JsonNode.Parse(golden.Body)!["detail"]!), (code.Error.Code, code.Error.Message));
    }

    [Fact]
    public async Task FileMap_maps_045()
    {
        var (client, server, _) = Reach();
        server.FileMapAnswer = RouteAnswer.Problem("context-filemap", "repo-outside-045");

        var map = await client.FileMap(_api, new FileMapQuery("elsewhere", Path: null, UnmappedOnly: false, Draft: false), Token);

        Assert.Equal(("SND-CONTEXT-045", (string)GoldenFiles.Node("context-filemap", "repo-outside-045")["detail"]!), (map.Error.Code, map.Error.Message));
    }

    [Fact]
    public async Task ReviewDecision_posts_one_body_with_the_wire_names_override_and_overrides()
    {
        var (client, server, handler) = Reach();
        var written = (string)GoldenFiles.Node("context-map-review", "post-accept-link")["result"]!["written"]![0]!;
        server.ReviewDecided = RouteAnswer.Api("context-map-review", "post-undo-list");

        _ = await client.ReviewDecision(_api, new ReviewDecisionRequest(DeskVerb.Undo, Overrides: [written]), Token);
        _ = await client.ReviewDecision(_api, new ReviewDecisionRequest(DeskVerb.Drop, Override: written), Token);

        Assert.All(handler.Requests, r => Assert.Equal(("POST", "/sindri/context/map/review", "human", "ui", null), (r.Method, r.Path, r.Caller, r.Client, r.Terminal)));
        Assert.Equal(2, server.ReviewCalls.Count);
        Assert.Equal([DeskVerb.Undo, DeskVerb.Drop], server.ReviewCalls.Select(c => c.Verb));
        Assert.Equal(["undo", "drop"], handler.ReviewBodies.Select(b => (string)JsonNode.Parse(b)!["verb"]!), StringComparer.Ordinal);   // the verb as it goes on the wire, not as the app's enum reads it back
        Assert.Equal([written], server.ReviewCalls[0].Overrides!, StringComparer.Ordinal);
        Assert.Equal(written, server.ReviewCalls[1].Override);
    }

    [Fact]
    public async Task ReviewDecision_maps_047_to_a_stale_row_refusal()
    {
        var (client, server, _) = Reach();
        server.ReviewDecided = RouteAnswer.Problem("context-map-review", "stale-047");

        var decision = await client.ReviewDecision(_api, new ReviewDecisionRequest(DeskVerb.Accept, Link: "edge:402b8bed42f6611c"), Token);

        Assert.Equal(("SND-CONTEXT-047", ErrorKind.Conflict, (string)GoldenFiles.Node("context-map-review", "stale-047")["detail"]!), (decision.Error.Code, decision.Error.Kind, decision.Error.Message));
    }

    [Fact]
    public async Task ReviewDecision_a_session_decision_reads_like_any_other()
    {
        var (client, server, _) = Reach();
        server.ReviewDecided = RouteAnswer.Api("context-map-review", "session-accept");

        var decision = await client.ReviewDecision(_api, new ReviewDecisionRequest(DeskVerb.Accept, Node: "code:x"), Token);

        Assert.True(decision.IsSuccess, decision.IsError ? decision.Error.Message : null);
    }

    [Fact]
    public async Task ReviewDecision_sends_once_and_never_retries()
    {
        var (client, _, handler) = Reach();
        handler.Override = (HttpStatusCode.ServiceUnavailable, RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-API-011", "views behind").Body);

        var decision = await client.ReviewDecision(_api, new ReviewDecisionRequest(DeskVerb.Accept, Node: "code:x"), Token);

        Assert.True(decision.IsError);
        _ = Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Context_reads_do_not_wait_on_the_views()
    {
        var (client, server, handler) = Reach();
        _ = await client.RebuildViews(_api, Token);   // a command: the next view query would carry its sequence

        _ = await client.Lenses(_api, new LensesQuery(Anchors("set-two-anchors"), Depth: null, Draft: false, MapVersion: null), Token);
        _ = await client.ReviewQueue(_api, limit: 5, Token);
        _ = await client.ReviewHistory(_api, limit: null, Token);
        _ = await client.MapStatus(_api, anchor: "feature:x", Token);
        _ = await client.FileMap(_api, new FileMapQuery("r", Path: null, UnmappedOnly: false, Draft: false), Token);
        _ = await client.Node(_api, "feature:x", Token);

        Assert.Equal(6, server.ContextCalls);
        Assert.All(handler.Requests.Where(r => r.Path.StartsWith("/sindri/context/", StringComparison.Ordinal)), r => Assert.Null(r.After));
    }

    [Fact]
    public async Task The_queue_history_and_status_read_their_goldens_with_their_query()
    {
        var (client, _, handler) = Reach();

        var queue = (await client.ReviewQueue(_api, limit: 5, Token)).Value;
        var history = (await client.ReviewHistory(_api, limit: 3, Token)).Value;
        var status = (await client.MapStatus(_api, anchor: "feature:x", Token)).Value;

        Assert.Equal((string)GoldenFiles.Node("context-map-review", "map-tabs-draft")["draft"]!, queue.Draft);
        Assert.Equal(GoldenFiles.Node("context-map-review-history", "after-accept-undo")["entries"]!.AsArray().Count, history.Entries.Count);
        Assert.Equal((int)GoldenFiles.Node("context-map", "map-tabs-anchor")["version"]!["number"]!, status.Version.Number);
        Assert.Equal(["?limit=5", "?limit=3", "?anchor=feature:x"], handler.Requests.Select(r => Uri.UnescapeDataString(r.Query)), StringComparer.Ordinal);
    }

    [Fact]
    public async Task CodeIndexStatus_reads_the_status_route_as_the_human_and_parses_the_golden()
    {
        var (client, _, handler) = Reach();

        var status = await client.CodeIndexStatus(_api, Token);

        var seen = Assert.Single(handler.Requests);
        Assert.Equal(("GET", "/sindri/code-index/status", string.Empty), (seen.Method, seen.Path, seen.Query));
        Assert.Equal(("human", "ui", A.HumanToken), (seen.Caller, seen.Client, seen.Token));
        var repo = Assert.Single(status.Value.Repos);
        Assert.Equal(("other", "ok"), (repo.Repo, repo.State));
        Assert.Equal(("structure", "ok"), (repo.Layers[0].Layer, repo.Layers[0].State));
    }

    [Fact]
    public async Task IndexStatus_reads_the_status_route_as_the_human_and_parses_the_per_source_golden()
    {
        var (client, _, handler) = Reach();

        var status = await client.IndexStatus(_api, Token);

        var seen = Assert.Single(handler.Requests);
        Assert.Equal(("GET", "/sindri/index/status", string.Empty), (seen.Method, seen.Path, seen.Query));
        Assert.Equal(("human", "ui", A.HumanToken), (seen.Caller, seen.Client, seen.Token));
        Assert.Equal(["github", "linear", "slack"], status.Value.Sources.Select(s => s.Source), StringComparer.Ordinal);
        var slack = status.Value.Sources[2];
        Assert.Equal(("failed", "SND-CONTEXT-041", 3), (slack.Pull, slack.LastError, slack.Failures));
        Assert.Null(status.Value.Sources[1].LastCommit);
    }

    [Fact]
    public async Task IndexStatus_reads_todays_shape_with_every_new_field_absent()
    {
        var (client, server, _) = Reach();
        server.IndexStatusAnswer = RouteAnswer.SpecShaped("index-status", "today");

        var status = await client.IndexStatus(_api, Token);

        Assert.All(status.Value.Sources, s => Assert.Equal((null, null, null), (s.Pull, s.Records, s.LastCommit)));
    }

    [Fact]
    public async Task The_publish_state_reads_its_golden_from_the_publish_route_and_never_waits_on_the_views()
    {
        var (client, _, handler) = Reach();
        _ = await client.RebuildViews(_api, Token);   // a command: the next view query would carry its sequence

        var publish = (await client.MapPublish(_api, Token)).Value;

        var seen = handler.Requests[^1];
        Assert.Equal(("GET", "/sindri/context/map/publish"), (seen.Method, seen.Path));
        Assert.Null(seen.After);
        Assert.Equal(((string)GoldenFiles.Node("context-map-publish", "published")["state"]!, 1), (publish.State, publish.Version));
        Assert.Equal(["Map v1 is current."], publish.Lines);
    }

    [Fact]
    public async Task No_map_yet_is_the_APIs_refusal_with_its_code()
    {
        var (client, server, _) = Reach();
        server.MapPublishAnswer = RouteAnswer.Problem(HttpStatusCode.NotFound, "SND-CONTEXT-030", "there is no map yet");

        var publish = await client.MapPublish(_api, Token);

        Assert.True(publish.IsError);
        Assert.Equal("SND-CONTEXT-030", publish.Error.Code);
    }
}
