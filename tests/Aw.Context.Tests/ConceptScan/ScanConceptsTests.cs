using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.ConceptScan;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Models;
using Aw.Context.Core.Results;
using Aw.Context.Testing.Graph;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.ConceptScan;

public sealed class ScanConceptsTests : IDisposable
{
    private const string Proposed = """{"concepts":[{"name":"Self scheduling","queries":["swap"]},{"name":"Payroll","queries":["payroll"]}]}""";
    private const string Concluded = """{"concepts":[{"name":"Self scheduling","members":["feature:shifts.swap","feature:shifts.open","feature:made.up"],"evidence":["src/shifts/swap.ts"],"confidence":0.8}]}""";

    private static readonly string[] _storedTerms = ["swap", "roster"];
    private static readonly ContextScope _scope = MapWorld.HumanWith(MapWorld.OnlyFixtureRepo);

    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static GraphBuilder Shifts() => new GraphBuilder()
        .Node("feature:shifts.swap", "Swap shifts")
        .Node("feature:shifts.open", "Open shifts")
        .Node("feature:pay.run", "Run payroll")
        .Edge("code:src/shifts/swap.ts", EdgeType.Implements, "feature:shifts.swap");

    private async Task<int> Seed(GraphSnapshot graph)
    {
        var saved = await _world.Store.SaveDraft(new MapImport(ContextRepoName.From(FixtureMap.Repo), "abc123", graph, []), _world.Clock.GetUtcNow(), MapWorld.None);
        Assert.True(saved.IsSuccess);
        return saved.Value.Number;
    }

    // The first pass proposes, the second concludes; the second prompt is the one that names the proposed concepts.
    private static string TwoPasses(ModelRequest request) => request.Prompt.Contains("Proposed concepts:", StringComparison.Ordinal) ? Concluded : Proposed;

    private Task<Kernel.Result<ConceptScanReport>> Scan(int? expected = null) => _world.Map.ScanConcepts(_scope, MapWorld.None, expected).AsTask();

    [Fact, Trait("Lane", "unit")]
    public async Task A_scan_stores_the_hints_the_second_pass_returns_for_the_draft()
    {
        var draft = await Seed(Shifts().Build());
        _world.Model.Responder = TwoPasses;

        var report = (await Scan()).Value;

        Assert.Equal(new ConceptScanReport(draft, OnDraft: true, ConceptScanOutcome.Scanned, Hints: 1, Searches: 2), report);
        var hint = Assert.Single((await _world.Hints.Read(draft, MapWorld.None)).Value);
        Assert.Equal("Self scheduling", hint.Name);
        Assert.Equal(["feature:shifts.swap", "feature:shifts.open"], hint.Members, StringComparer.Ordinal);   // 'feature:made.up' is no feature of the draft
        Assert.Equal(["src/shifts/swap.ts"], hint.Evidence, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_terms_a_build_stored_on_the_concepts_join_the_first_prompt()
    {
        var graph = Shifts().Build();
        var attributes = System.Text.Json.JsonSerializer.SerializeToElement(new Dictionary<string, object?>(StringComparer.Ordinal) { ["summary"] = "", ["source"] = "derived", ["seed"] = "feature:shifts.swap", ["terms"] = _storedTerms });
        var concept = new GraphNode(NodeKey.From("concept:shifts"), "Shifts", Provenance.Deterministic, 0.5, Evidence: null, attributes);
        var edge = new GraphEdge(NodeKey.From("feature:shifts.swap"), concept.Key, EdgeType.BelongsTo);
        _ = await Seed(graph with { Nodes = [.. graph.Nodes, concept], Edges = [.. graph.Edges, edge] });
        _world.Model.Responder = TwoPasses;

        _ = await Scan();

        Assert.Contains("term swap: feature:shifts.swap", _world.Model.Requests[0].Prompt, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Both_calls_are_concept_calls_within_the_budget()
    {
        _ = await Seed(Shifts().Build());
        _world.Model.Responder = TwoPasses;

        _ = await Scan();

        Assert.Equal(2, _world.Model.Requests.Count);
        Assert.All(_world.Model.Requests, static r =>
        {
            Assert.Equal(ModelPurpose.Concepts, r.Purpose);
            Assert.Equal(ConceptScanLimits.MaxOutputTokens, r.MaxTokens);
            Assert.Equal(ConceptScanLimits.CallTimeout, r.Budget);
            Assert.True(r.Prompt.Length <= ConceptScanLimits.MaxInputChars);
        });
    }

    [Fact, Trait("Lane", "unit")]
    public async Task What_the_searches_found_reaches_the_second_prompt()
    {
        _ = await Seed(Shifts().Build());
        _world.Model.Responder = TwoPasses;

        _ = await Scan();

        var second = _world.Model.Requests[1].Prompt;
        Assert.Contains("search for swap:", second, StringComparison.Ordinal);
        Assert.Contains("feature:shifts.swap | Swap shifts", second, StringComparison.Ordinal);
        Assert.Contains("search for payroll:", second, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_first_prompt_carries_the_features_and_their_files()
    {
        _ = await Seed(Shifts().Build());
        _world.Model.Responder = TwoPasses;

        _ = await Scan();

        Assert.Contains("feature:shifts.swap | Swap shifts | src/shifts/swap.ts", _world.Model.Requests[0].Prompt, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_scan_changes_nothing_in_the_map()
    {
        _ = await Seed(Shifts().Build());
        _world.Model.Responder = TwoPasses;
        var before = (await _world.Store.Snapshot(draft: true, MapWorld.None)).Value!;
        var overrides = (await _world.Store.Overrides(MapWorld.None)).Value.Count;

        _ = await Scan();

        var after = (await _world.Store.Snapshot(draft: true, MapWorld.None)).Value!;
        Assert.Equal(before.Graph.Nodes.Count, after.Graph.Nodes.Count);
        Assert.Equal(before.Graph.Edges.Count, after.Graph.Edges.Count);
        Assert.Equal(overrides, (await _world.Store.Overrides(MapWorld.None)).Value.Count);
        Assert.Null((await _world.Store.Snapshot(draft: false, MapWorld.None)).Value);   // nothing was promoted
    }

    [Fact, Trait("Lane", "unit")]
    public async Task When_no_model_answers_the_scan_ends_with_an_outcome_and_stores_nothing()
    {
        var draft = await Seed(Shifts().Build());
        _world.Model.Fail = _ => ContextErrors.LabelingUnavailable.With("no model");

        var report = (await Scan()).Value;

        Assert.Equal(new ConceptScanReport(draft, OnDraft: true, ConceptScanOutcome.ModelUnavailable, Hints: 0, Searches: 0), report);
        Assert.Equal(0, _world.Hints.Saves);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task When_the_second_pass_fails_nothing_is_stored()
    {
        var draft = await Seed(Shifts().Build());
        _world.Model.Responder = TwoPasses;
        _world.Model.Fail = static r => r.Prompt.Contains("Proposed concepts:", StringComparison.Ordinal) ? ContextErrors.LabelingUnavailable.With("timed out") : null;

        var report = (await Scan()).Value;

        Assert.Equal(ConceptScanOutcome.ModelUnavailable, report.Outcome);
        Assert.Equal(draft, report.Version);
        Assert.Equal(0, _world.Hints.Saves);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_reply_that_is_not_JSON_finds_no_concepts_and_the_second_call_is_never_made()
    {
        _ = await Seed(Shifts().Build());
        _world.Model.Answer = "I could not find any concepts";

        var report = (await Scan()).Value;

        Assert.Equal(ConceptScanOutcome.NoConcepts, report.Outcome);
        _ = Assert.Single(_world.Model.Requests);
        Assert.Equal(0, _world.Hints.Saves);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_second_reply_whose_members_are_all_unknown_stores_an_empty_list_and_says_no_concepts()
    {
        var draft = await Seed(Shifts().Build());
        _world.Model.Responder = r => string.Equals(TwoPasses(r), Concluded, StringComparison.Ordinal) ? """{"concepts":[{"name":"Ghost","members":["feature:made.up"],"confidence":0.9}]}""" : Proposed;

        var report = (await Scan()).Value;

        Assert.Equal(ConceptScanOutcome.NoConcepts, report.Outcome);
        Assert.Empty((await _world.Hints.Read(draft, MapWorld.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_draft_without_features_has_nothing_to_scan_and_no_model_is_asked()
    {
        var draft = await Seed(new GraphBuilder().Node("surface:command/split", "Split pane").Build());

        var report = (await Scan()).Value;

        Assert.Equal(new ConceptScanReport(draft, OnDraft: true, ConceptScanOutcome.NothingToScan, Hints: 0, Searches: 0), report);
        Assert.Empty(_world.Model.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_no_draft_open_the_scan_reads_the_current_version_and_stores_its_hints_beside_it()
    {
        var saved = await _world.Store.SaveCurrent(new MapImport(ContextRepoName.From(FixtureMap.Repo), "abc123", Shifts().Build(), []), _world.Clock.GetUtcNow(), MapWorld.None);
        _world.Model.Responder = TwoPasses;

        var report = (await Scan()).Value;

        Assert.Equal(new ConceptScanReport(saved.Value.Number, OnDraft: false, ConceptScanOutcome.Scanned, Hints: 1, Searches: 2), report);
        _ = Assert.Single((await _world.Hints.Read(saved.Value.Number, MapWorld.None)).Value);
        Assert.Contains("feature:shifts.swap | Swap shifts", _world.Model.Requests[1].Prompt, StringComparison.Ordinal);   // the searches read the current map
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_no_draft_and_no_current_version_the_scan_is_refused_with_the_no_draft_code()
    {
        var result = await Scan();

        Assert.True(result.IsError);
        Assert.Equal(ContextErrors.NoDraftMap.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_draft_that_is_not_the_one_expected_is_refused_and_nothing_is_asked()
    {
        var draft = await Seed(Shifts().Build());

        var result = await Scan(expected: draft + 1);

        Assert.True(result.IsError);
        Assert.Equal(ContextErrors.DraftChanged.Code, result.Error.Code);
        Assert.Empty(_world.Model.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_big_map_is_cut_to_fit_the_prompt_limit()
    {
        var graph = Enumerable.Range(0, 3_000).Aggregate(new GraphBuilder(), static (b, i) => b.Node(string.Create(CultureInfo.InvariantCulture, $"feature:area.f{i}"), string.Create(CultureInfo.InvariantCulture, $"A long enough label to fill the prompt {i}")));
        _ = await Seed(graph.Build());
        _world.Model.Responder = TwoPasses;

        _ = await Scan();

        Assert.All(_world.Model.Requests, static r => Assert.True(r.Prompt.Length <= ConceptScanLimits.MaxInputChars, "a prompt passed the input limit"));
        Assert.Contains("more lines not shown", _world.Model.Requests[0].Prompt, StringComparison.Ordinal);
    }
}
