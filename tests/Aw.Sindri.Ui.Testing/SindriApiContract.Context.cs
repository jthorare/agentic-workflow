using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The contract (<see cref="SindriApiContract"/>, continued): the context routes answer as the others do, fake and client alike.</summary>
public abstract partial class SindriApiContract
{
    private static string[] AnchorKeys(string scenario) => [.. GoldenFiles.Node("context-lenses", scenario)["anchors"]!.AsArray().Select(a => (string)a!["key"]!)];

    [Fact]
    public async Task Lenses_answers_the_golden_for_a_known_anchor_set()
    {
        var api = Reach(new FakeSindriApi());
        var one = (string)GoldenFiles.Node("context-lenses", "map-tabs")["anchor"]!["key"]!;

        var single = (await api.Lenses(_api, new LensesQuery([one], Depth: null, Draft: false, MapVersion: null), Token)).Value;
        var set = (await api.Lenses(_api, new LensesQuery(AnchorKeys("set-two-anchors"), Depth: null, Draft: false, MapVersion: null), Token)).Value;

        Expect.Sequence([one], single.Anchors.Select(a => a.Key));
        Expect.Sequence(AnchorKeys("set-two-anchors"), set.Anchors.Select(a => a.Key));
        Expect.Equal((1, 2), (single.SchemaVersion, set.SchemaVersion));
    }

    [Fact]
    public async Task Lenses_answers_003_for_an_unknown_anchor()
    {
        var api = Reach(new FakeSindriApi());

        var lenses = await api.Lenses(_api, new LensesQuery(["feature:tabs.nope"], Depth: null, Draft: false, MapVersion: null), Token);

        var served = GoldenFiles.Node("context-lenses", "unknown-anchor-003");
        Expect.Equal(((string)served["title"]!, (string)served["detail"]!), (lenses.Error.Code, lenses.Error.Message));
    }

    [Fact]
    public async Task ReviewDecision_applies_a_decision_to_the_queue()
    {
        var api = Reach(new FakeSindriApi());
        var queue = GoldenFiles.Node("context-map-review", "map-tabs-draft");
        var entry = queue["entries"]![0]!;

        var accepted = (await api.ReviewDecision(_api, new ReviewDecisionRequest(DeskVerb.Accept, Node: (string)entry["node"]!, Link: (string?)entry["link"]), Token)).Value;
        var after = (await api.ReviewQueue(_api, limit: null, Token)).Value;

        var written = GoldenFiles.Node("context-map-review", "post-accept-link")["result"]!["written"]!.AsArray().Select(w => (string)w!);
        Expect.Sequence(written, accepted.Result!.Written);
        Expect.Equal((queue["entries"]!.AsArray().Count - 1, (int)queue["undecided"]! - 1), (after.Entries.Count, after.Undecided));
        Expect.True(after.Entries.All(e => e.Node != (string)entry["node"]!));

        _ = await api.ReviewDecision(_api, new ReviewDecisionRequest(DeskVerb.Undo, Overrides: [.. accepted.Result.Written]), Token);
        var undone = (await api.ReviewQueue(_api, limit: null, Token)).Value;

        Expect.Equal((queue["entries"]!.AsArray().Count, (int)queue["undecided"]!), (undone.Entries.Count, undone.Undecided));
    }

    [Fact]
    public async Task FileMap_lists_children_of_a_path()
    {
        var api = Reach(new FakeSindriApi());
        var root = GoldenFiles.Node("context-filemap", "root");
        var tabs = GoldenFiles.Node("context-filemap", "src-tabs");
        var unmapped = GoldenFiles.Node("context-filemap", "src-unmapped");
        var repo = (string)root["repo"]!;

        var top = (await api.FileMap(_api, new FileMapQuery(repo, Path: null, UnmappedOnly: false, Draft: false), Token)).Value;
        var children = (await api.FileMap(_api, new FileMapQuery(repo, Path: (string)tabs["path"]!, UnmappedOnly: false, Draft: false), Token)).Value;
        var only = (await api.FileMap(_api, new FileMapQuery(repo, Path: (string)unmapped["path"]!, UnmappedOnly: true, Draft: false), Token)).Value;

        Expect.Sequence(Names(root), top.Entries.Select(e => e.Name));
        Expect.Sequence(Names(tabs), children.Entries.Select(e => e.Name));
        Expect.Sequence(Names(unmapped), only.Entries.Select(e => e.Name));
    }

    [Fact]
    public async Task Code_answers_the_callers_and_callees_of_a_known_anchor()
    {
        var api = Reach(new FakeSindriApi());
        var served = GoldenFiles.Node("context-code", "edges-and-lexical");

        var code = (await api.Code(_api, new CodeQuery([(string)served["anchors"]![0]!["key"]!], Draft: false, MapVersion: null), Token)).Value;

        Expect.Equal(((string)served["mapVersion"]!, (string)served["callers"]!["state"]!, (string)served["callees"]!["reason"]!), (code.MapVersion, code.Callers.State, code.Callees.Reason));
    }

    [Fact]
    public async Task Code_answers_003_for_an_unknown_anchor()
    {
        var api = Reach(new FakeSindriApi());

        var code = await api.Code(_api, new CodeQuery(["feature:nope"], Draft: false, MapVersion: null), Token);

        Expect.Equal("SND-CONTEXT-003", code.Error.Code);
    }

    // The server answers a set with some unresolved anchors as a 200 that names them in missingAnchors; only "nothing resolves" is 003.
    [Fact]
    public async Task Lenses_answers_200_with_missingAnchors_when_only_some_of_a_set_resolves()
    {
        var api = Reach(new FakeSindriApi());
        var served = GoldenFiles.Node("context-lenses", "set-missing-anchor");

        var lenses = (await api.Lenses(_api, new LensesQuery([.. AnchorKeys("set-missing-anchor"), .. served["missingAnchors"]!.AsArray().Select(m => (string)m!)], Depth: null, Draft: false, MapVersion: null), Token)).Value;

        Expect.Sequence(served["missingAnchors"]!.AsArray().Select(m => (string)m!), lenses.MissingAnchors);
        Expect.Sequence(AnchorKeys("set-missing-anchor"), lenses.Anchors.Select(a => a.Key));
    }

    // The fake holds one partly-known golden; it never answers it for a different set, so a reply always names what was asked.
    [Fact]
    public async Task Lenses_does_not_serve_a_partly_known_set_the_golden_does_not_hold()
    {
        var api = Reach(new FakeSindriApi());
        var known = AnchorKeys("set-two-anchors");

        var other = await api.Lenses(_api, new LensesQuery([known[1], "feature:tabs.nope"], Depth: null, Draft: false, MapVersion: null), Token);
        var wider = await api.Lenses(_api, new LensesQuery([.. known, "feature:tabs.nope"], Depth: null, Draft: false, MapVersion: null), Token);

        // Not 003 (the real server answers 200 with missingAnchors): the fake's "not served" answer, so a test relying on it fails loudly.
        Expect.Equal((ErrorKind.Unavailable, ErrorKind.Unavailable), (other.Error.Kind, wider.Error.Kind));   // an unserved route reads as unavailable
        Expect.True(other.Error.Code != "SND-CONTEXT-003" && wider.Error.Code != "SND-CONTEXT-003");
    }

    [Fact]
    public async Task Lenses_answers_003_for_a_known_anchor_alone_when_no_single_anchor_golden_holds_it()
    {
        var api = Reach(new FakeSindriApi());

        var single = await api.Lenses(_api, new LensesQuery([AnchorKeys("set-two-anchors")[1]], Depth: null, Draft: false, MapVersion: null), Token);

        Expect.Equal("SND-CONTEXT-003", single.Error.Code);
    }

    [Fact]
    public async Task A_released_gate_lets_the_read_answer_its_golden()
    {
        var root = GoldenFiles.Node("context-filemap", "root");
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = new FakeSindriApi { FileMapGate = gate };
        var read = Reach(server).FileMap(_api, new FileMapQuery((string)root["repo"]!, Path: null, UnmappedOnly: false, Draft: false), Token);
        Expect.True(!read.IsCompleted);

        gate.SetResult();

        Expect.Sequence(Names(root), (await read.WaitAsync(TimeSpan.FromSeconds(10), Token)).Value.Entries.Select(e => e.Name));
    }

    [Fact]
    public async Task Lenses_answers_003_for_a_single_anchor_no_golden_names_and_for_a_set_where_nothing_resolves()
    {
        var api = Reach(new FakeSindriApi());

        var single = await api.Lenses(_api, new LensesQuery(["feature:tabs.nope"], Depth: null, Draft: false, MapVersion: null), Token);
        var none = await api.Lenses(_api, new LensesQuery(["feature:a.nope", "feature:b.nope"], Depth: null, Draft: false, MapVersion: null), Token);

        Expect.Equal("SND-CONTEXT-003", single.Error.Code);
        Expect.Equal("SND-CONTEXT-003", none.Error.Code);
    }

    // A read in flight that the caller cancels completes as cancelled, so a later stage can test its stale-response races.
    [Fact]
    public async Task A_gated_read_that_is_cancelled_completes_as_cancelled()
    {
        var server = new FakeSindriApi { LensesGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token);
        cancel.CancelAfter(TimeSpan.FromSeconds(20));   // a bound, not the test: a hang fails instead of stalling the lane
        var read = Reach(server).Lenses(_api, new LensesQuery(AnchorKeys("set-two-anchors"), Depth: null, Draft: false, MapVersion: null), cancel.Token);
        Expect.True(!read.IsCompleted);

        await cancel.CancelAsync();

        var cancelled = false;
        try
        {
            _ = await read.WaitAsync(TimeSpan.FromSeconds(10), Token);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        Expect.True(cancelled);
    }

    private static string[] Names(JsonNode map) => [.. map["entries"]!.AsArray().Select(e => (string)e!["name"]!)];

    [Fact]
    public async Task WholeMap_answers_the_golden_with_its_counts_areas_nodes_and_edges()
    {
        var api = Reach(new FakeSindriApi());
        var golden = GoldenFiles.Node("context-whole", "nodes-small");

        var map = (await api.WholeMap(_api, new WholeMapQuery("auto", Area: null, [], MapVersion: null, Draft: false), Token)).Value;

        Expect.Equal(((string)golden["mapVersion"]!, (string)golden["detail"]!, (int)golden["counts"]!["nodes"]!), (map.MapVersion, map.Detail, map.Counts.Nodes));
        Expect.Sequence(golden["areas"]!.AsArray().Select(a => (string)a!["key"]!), map.Areas.Select(a => a.Key));
        Expect.Equal((golden["nodes"]!.AsArray().Count, golden["edges"]!.AsArray().Count), (map.Nodes!.Count, map.Edges!.Count));   // why: a node-level answer has both
    }

    [Fact]
    public async Task WholeMap_answers_an_over_budget_map_by_area_with_the_reason_and_no_nodes()
    {
        var api = Reach(new FakeSindriApi { WholeMapAnswer = _ => new RouteAnswer(System.Net.HttpStatusCode.OK, GoldenFiles.Api("context-whole", "areas-over-budget")) });

        var map = (await api.WholeMap(_api, new WholeMapQuery("nodes", Area: null, [], MapVersion: null, Draft: false), Token)).Value;

        Expect.Equal(("areas", "over-budget", true), (map.Detail, map.Fallback, map.Nodes is null));
    }

    [Fact]
    public async Task WholeMap_sends_the_detail_the_area_and_every_key_to_locate()
    {
        var server = new FakeSindriApi();
        var api = Reach(server);

        _ = await api.WholeMap(_api, new WholeMapQuery("areas", "feature:tabs", ["code:a.ts", "code:b.ts"], "v2", Draft: true), Token);

        var asked = server.WholeMapAsked[^1];
        Expect.Equal(("areas", "feature:tabs", "v2", true), (asked.Detail, asked.Area, asked.MapVersion, asked.Draft));
        Expect.Sequence(["code:a.ts", "code:b.ts"], asked.Locate);
    }
}
