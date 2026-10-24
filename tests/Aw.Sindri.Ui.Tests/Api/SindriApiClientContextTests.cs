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
    public async Task Lenses_sends_each_anchor_as_a_repeated_query_parameter()
    {
        var (client, _, handler) = Reach();
        var anchors = Anchors("set-two-anchors");

        var lenses = await client.Lenses(_api, new LensesQuery(anchors, Depth: 1, Draft: true, MapVersion: "v1"), Token);

        _ = lenses;
        var seen = Assert.Single(handler.Requests);
        Assert.Equal(("GET", "/sindri/context/lenses"), (seen.Method, seen.Path));
        Assert.Equal($"?anchor={anchors[0]}&anchor={anchors[1]}&depth=1&draft=true&mapVersion=v1", Uri.UnescapeDataString(seen.Query));
    }

    [Fact]
    public async Task Lenses_sends_only_what_it_was_asked()
    {
        var (client, _, handler) = Reach();
        var anchor = Anchors("set-two-anchors")[0];

        _ = await client.Lenses(_api, new LensesQuery([anchor], Depth: null, Draft: false, MapVersion: null), Token);

        Assert.Equal($"?anchor={anchor}", Query(handler));
    }

    // Keys hold ':', '/' and '#': they travel as escaped query values and never become a path segment.
    [Fact]
    public async Task Keys_travel_in_query_strings_never_the_path()
    {
        var (client, _, handler) = Reach();
        var key = (string)GoldenFiles.Node("context-node", "tab-manager")["node"]!["key"]! + "/x#y";

        _ = await client.Node(_api, key, Token);
        _ = await client.Lenses(_api, new LensesQuery([key], Depth: null, Draft: false, MapVersion: null), Token);

        Assert.Equal(["/sindri/context/node", "/sindri/context/lenses"], handler.Requests.Select(r => r.Path), StringComparer.Ordinal);
        Assert.All(handler.Requests, r =>
        {
            Assert.Contains(Uri.EscapeDataString(key), r.Query, StringComparison.Ordinal);
            Assert.DoesNotContain('#', r.Query);
        });
    }

    [Theory]
    [InlineData("unknown-anchor-003", "SND-CONTEXT-003", ErrorKind.NotFound, "suggestedAnchors")]
    [InlineData("ambiguous-037", "SND-CONTEXT-037", ErrorKind.Conflict, "candidates")]
    [InlineData("no-draft-044", "SND-CONTEXT-044", null, null)]
    [InlineData("bad-depth-006", "SND-CONTEXT-006", null, null)]
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
    public async Task Code_sends_each_anchor_the_draft_flag_and_the_map_version()
    {
        var (client, _, handler) = Reach();
        var key = (string)GoldenFiles.Node("context-code", "edges-and-lexical")["anchors"]![0]!["key"]!;

        _ = await client.Code(_api, new CodeQuery([key, "feature:other"], Draft: true, MapVersion: "v2"), Token);

        Assert.Equal("/sindri/context/code", Assert.Single(handler.Requests).Path);
        Assert.Equal($"?anchor={key}&anchor=feature:other&draft=true&mapVersion=v2", Query(handler));
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
    public async Task ReviewDecision_a_session_refusal_reads_as_SND_AUTH_002()
    {
        var (client, server, _) = Reach();
        server.ReviewDecided = RouteAnswer.Problem("context-map-review", "session-auth-002");

        var decision = await client.ReviewDecision(_api, new ReviewDecisionRequest(DeskVerb.Accept, Node: "code:x"), Token);

        Assert.Equal(("SND-AUTH-002", ErrorKind.Forbidden), (decision.Error.Code, decision.Error.Kind));
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
