using System.Globalization;
using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Records;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Symbols;
using Aw.Context.Features.BuildMap;
using Aw.Context.Testing.Records;
using Aw.Context.Testing.Symbols;

namespace Aw.Context.Tests.BuildMap;

/// <summary>Spec 2026-10-27 §7.1 and §7.5: the build reads docs and tracker records from the store; cited paths and PR files become edges, what doesn't resolve is a number in the report.</summary>
public sealed class MapRecordSourcesTests
{
    private static readonly ContextRepoName _app = ContextRepoName.From("app");
    private static readonly ContextRepoName _lib = ContextRepoName.From("lib");
    private static readonly string[] _docGlobs = ["docs/**/*.md"];
    private static readonly string[] _names = ["AGENTS.md"];
    private static readonly string[] _appPaths = ["docs/guide.md", "src/billing/AGENTS.md", "src/billing/Invoice.cs", "src/billing/Tax.cs", "src/common/Both.cs"];
    private static readonly string[] _libPaths = ["src/common/Both.cs", "src/shared/Util.cs"];

    private static RecordWrite Doc(string path, string version, params string[] texts) =>
        RecordSamples.Write(RecordSamples.Record(RepoDocs.Key(_app, path).Value, version, RepoDocs.Id) with { Kind = RecordKind.Doc, Repo = _app, Title = path }, texts);

    private static RecordWrite Tracker(string key, string text, RecordKind kind = RecordKind.Issue, ContextRepoName? repo = null, params RecordLink[] links) =>
        RecordSamples.Write(RecordSamples.Record(key, "v1", RecordSamples.Github) with { Kind = kind, Repo = repo, Links = links }, text);

    private static async Task<FakeRecordStore> Store(params RecordWrite[] writes)
    {
        var store = new FakeRecordStore();
        foreach (var source in writes.GroupBy(w => w.Record.Source))
        {
            _ = await store.Commit(RecordSamples.Batch(source.Key, 1, [.. source]), CancellationToken.None);
        }

        return store;
    }

    private static MapRecordInput Input(bool lib = false) => new(
        [new MapRepoFiles(_app, _appPaths, new Dictionary<string, string>(StringComparer.Ordinal) { ["docs/guide.md"] = "b1", ["src/billing/AGENTS.md"] = "b2" }), .. lib ? [new MapRepoFiles(_lib, _libPaths, new Dictionary<string, string>(StringComparer.Ordinal))] : Array.Empty<MapRepoFiles>()],
        _docGlobs, _names, RecordSamples.Now);

    private static FakeCodeSymbolSource Index(params SymbolSite[] symbols) =>
        new([new FakeIndexedRepo(_app, CodeIndexState.Ok, "7c3e1a2f4b5d6e7081920a1b2c3d4e5f60718293", symbols, [])]);

    private static SymbolSite Class(string name, string path) => new($"{path}#{name}", name, "class", path, 1, 40, Signature: null, Exported: true);

    private static async Task<MapRecordReading> Read(FakeRecordStore store, bool lib = false, FakeCodeSymbolSource? symbols = null)
    {
        var result = await new MapRecordSources(store, symbols ?? new FakeCodeSymbolSource([])).Read(Input(lib), CancellationToken.None);
        return result.Value;
    }

    private static void AssertNote(MapRecordReading reading, string note) => Assert.Contains(reading.Notes, n => string.Equals(n, note, StringComparison.Ordinal));

    private static string Doc1(string path) => "record:" + RepoDocs.Key(_app, path).Value;

    private static List<string> Targets(MapRecordReading reading, string from) =>
        [.. reading.Map.Edges.Where(e => e.From.Value == from && e.Type == EdgeType.Mentions).Select(e => e.To.Value).Order(StringComparer.Ordinal)];

    [Fact, Trait("Lane", "unit")]
    public async Task A_doc_in_the_path_list_is_a_node_even_when_the_store_has_no_row_for_it_and_the_report_counts_it()
    {
        var reading = await Read(await Store(Doc("src/billing/AGENTS.md", "b2", "no paths here")));

        Assert.Equal([Doc1("docs/guide.md"), Doc1("src/billing/AGENTS.md")], reading.Map.Nodes.Select(n => n.Key.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        AssertNote(reading, "records: 1 docs not yet synced");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_fresh_doc_cites_files_as_deterministic_mentions_at_full_confidence_and_the_unresolved_are_counted()
    {
        var reading = await Read(await Store(Doc("docs/guide.md", "b1", "Start at `src/billing/Invoice.cs`", "then src/billing/Tax.cs:12 and `src/billing/Gone.cs`.")));

        Assert.Equal(["code:src/billing/Invoice.cs", "code:src/billing/Tax.cs"], Targets(reading, Doc1("docs/guide.md")), StringComparer.Ordinal);
        Assert.All(reading.Map.Edges, e => Assert.Equal((Provenance.Deterministic, 1.0), (e.Provenance, e.Confidence)));
        Assert.Equal(RepoDocs.Key(_app, "docs/guide.md"), Assert.Single(reading.Map.Edges, e => e.To.Value == "code:src/billing/Tax.cs").EvidenceOrEmpty.Single().Key);
        AssertNote(reading, "citations: 2 resolved, 1 unresolved (1 missing, 0 ambiguous)");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_doc_older_than_its_blob_at_head_keeps_its_node_but_its_citations_are_skipped_and_counted()
    {
        var reading = await Read(await Store(Doc("docs/guide.md", "old-blob", "see `src/billing/Invoice.cs`")));

        Assert.Contains(reading.Map.Nodes, n => n.Key.Value == Doc1("docs/guide.md"));
        Assert.Empty(reading.Map.Edges);
        AssertNote(reading, "records: 1 docs older than HEAD");
        Assert.DoesNotContain(reading.Notes, n => n.StartsWith("citations:", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_doc_the_scrubber_withheld_keeps_its_node_with_no_edges_and_is_not_called_stale()
    {
        var blocked = RecordSamples.Record(RepoDocs.Key(_app, "docs/guide.md").Value, "b1", RepoDocs.Id, blocked: true) with { Kind = RecordKind.Doc, Repo = _app };
        var reading = await Read(await Store(new RecordWrite(blocked, [])));

        Assert.Contains(reading.Map.Nodes, n => n.Key.Value == Doc1("docs/guide.md"));
        Assert.Empty(reading.Map.Edges);
        Assert.DoesNotContain(reading.Notes, n => n.Contains("older than HEAD", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_tracker_record_with_no_edge_into_the_map_stays_in_the_store_and_is_not_a_node()
    {
        var reading = await Read(await Store(Tracker("github:issue/acme/app/1", "nothing to cite")));

        Assert.DoesNotContain(reading.Map.Nodes, n => n.Key.Value.Contains("github:", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_tracker_record_that_cites_a_file_becomes_a_node_keyed_by_its_record_key_with_a_mentions_edge()
    {
        var reading = await Read(await Store(Tracker("github:issue/acme/app/2", "breaks in `src/billing/Tax.cs`", repo: _app)));

        Assert.Contains(reading.Map.Nodes, n => n.Key.Value == "record:github:issue/acme/app/2");
        Assert.Equal(["code:src/billing/Tax.cs"], Targets(reading, "record:github:issue/acme/app/2"), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_citation_is_resolved_against_the_records_own_repo_then_the_one_other_repo_that_has_it_and_is_ambiguous_when_several_do()
    {
        var store = await Store(
            Doc("docs/guide.md", "b1", "uses `src/shared/Util.cs`"),
            Tracker("github:issue/acme/app/3", "see `src/common/Both.cs` and `src/shared/Util.cs`"),
            Tracker("github:issue/acme/app/4", "see `src/common/Both.cs`", repo: _app));
        var reading = await Read(store, lib: true);

        Assert.Equal(["code:src/shared/Util.cs"], Targets(reading, "record:github:issue/acme/app/3"), StringComparer.Ordinal);
        Assert.Equal(["code:src/common/Both.cs"], Targets(reading, "record:github:issue/acme/app/4"), StringComparer.Ordinal);
        Assert.Equal(["code:src/shared/Util.cs"], Targets(reading, Doc1("docs/guide.md")), StringComparer.Ordinal);
        AssertNote(reading, "citations: 3 resolved, 1 unresolved (0 missing, 1 ambiguous)");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pull_request_touches_the_files_it_changed_and_a_path_since_removed_is_its_own_count()
    {
        var pr = Tracker("github:pr/acme/app/7", "Fix tax", RecordKind.Pr, _app,
            new RecordLink("path:app/src/billing/Tax.cs", RecordLinkKind.Touches, Title: null),
            new RecordLink("path:app/src/billing/Invoice.cs", RecordLinkKind.Touches, Title: null),
            new RecordLink("path:app/src/billing/Deleted.cs", RecordLinkKind.Touches, Title: null));
        var reading = await Read(await Store(pr));

        Assert.Equal(["code:src/billing/Invoice.cs", "code:src/billing/Tax.cs"], Targets(reading, "record:github:pr/acme/app/7"), StringComparer.Ordinal);
        AssertNote(reading, "touched files since removed: 1");
        Assert.DoesNotContain(reading.Notes, n => n.StartsWith("citations:", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pull_request_over_the_cap_gets_no_file_edges_and_is_counted_with_its_paths()
    {
        var paths = Enumerable.Range(0, 60).Select(i => string.Create(CultureInfo.InvariantCulture, $"src/gen/F{i:D2}.cs")).ToList();
        var pr = Tracker("github:pr/acme/app/8", "Big rename", RecordKind.Pr, _app, [.. paths.Select(p => new RecordLink("path:app/" + p, RecordLinkKind.Touches, Title: null))]);
        var input = new MapRecordInput([new MapRepoFiles(_app, paths, new Dictionary<string, string>(StringComparer.Ordinal))], _docGlobs, _names, RecordSamples.Now);
        var reading = (await new MapRecordSources(await Store(pr), new FakeCodeSymbolSource([])).Read(input, CancellationToken.None)).Value;

        Assert.Empty(reading.Map.Edges);
        Assert.DoesNotContain(reading.Map.Nodes, n => n.Key.Value.Contains("pr/acme/app/8", StringComparison.Ordinal));
        AssertNote(reading, "records over the file cap: 1 (60 paths)");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pull_request_with_exactly_the_cap_keeps_every_file_edge()
    {
        var paths = Enumerable.Range(0, MapRecordSources.TouchCap).Select(i => string.Create(CultureInfo.InvariantCulture, $"src/gen/F{i:D2}.cs")).ToList();
        var pr = Tracker("github:pr/acme/app/10", "Rename", RecordKind.Pr, _app, [.. paths.Select(p => new RecordLink("path:app/" + p, RecordLinkKind.Touches, Title: null))]);
        var input = new MapRecordInput([new MapRepoFiles(_app, paths, new Dictionary<string, string>(StringComparer.Ordinal))], _docGlobs, _names, RecordSamples.Now);
        var reading = (await new MapRecordSources(await Store(pr), new FakeCodeSymbolSource([])).Read(input, CancellationToken.None)).Value;

        Assert.Equal(MapRecordSources.TouchCap, Targets(reading, "record:github:pr/acme/app/10").Count);
        Assert.DoesNotContain(reading.Notes, n => n.StartsWith("records over the file cap:", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_cited_symbol_the_code_index_knows_is_a_mentions_edge_to_the_file_that_defines_it()
    {
        var store = await Store(Doc("docs/guide.md", "b1", "The `TaxCalculator` does it."));
        var reading = await Read(store, symbols: Index(Class("TaxCalculator", "src/billing/Tax.cs")));

        Assert.Equal(["code:src/billing/Tax.cs"], Targets(reading, Doc1("docs/guide.md")), StringComparer.Ordinal);
        AssertNote(reading, "citations: 1 resolved, 0 unresolved (0 missing, 0 ambiguous)");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_cited_symbol_stays_unresolved_and_counted_when_the_code_index_is_off_or_does_not_know_it()
    {
        var store = await Store(Doc("docs/guide.md", "b1", "The `TaxCalculator` and `Unknown_Thing`."));
        var off = await Read(store);
        var unknown = await Read(store, symbols: Index(Class("TaxCalculator", "src/billing/Tax.cs")));

        Assert.Empty(off.Map.Edges);
        AssertNote(off, "citations: 0 resolved, 2 unresolved (2 missing, 0 ambiguous)");
        Assert.Equal(["code:src/billing/Tax.cs"], Targets(unknown, Doc1("docs/guide.md")), StringComparer.Ordinal);
        AssertNote(unknown, "citations: 1 resolved, 1 unresolved (1 missing, 0 ambiguous)");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_symbol_defined_in_two_files_is_ambiguous_and_makes_no_edge_and_a_plain_word_is_not_looked_up()
    {
        var store = await Store(Doc("docs/guide.md", "b1", "The `Handler` and `true` and `value`."));
        var reading = await Read(store, symbols: Index(Class("Handler", "src/billing/Tax.cs"), Class("Handler", "src/billing/Invoice.cs")));

        Assert.Empty(reading.Map.Edges);
        AssertNote(reading, "citations: 0 resolved, 1 unresolved (0 missing, 1 ambiguous)");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lower_case_name_with_a_call_or_member_shape_is_looked_up_by_its_last_part()
    {
        var store = await Store(Doc("docs/guide.md", "b1", "Call `config.load` then `render()`, never `true` or `value`."));
        var reading = await Read(store, symbols: Index(Class("load", "src/billing/Tax.cs"), Class("render", "src/billing/Invoice.cs")));

        Assert.Equal(["code:src/billing/Invoice.cs", "code:src/billing/Tax.cs"], [.. reading.Map.Edges.Select(e => e.To.Value).Order(StringComparer.Ordinal)], StringComparer.Ordinal);
        AssertNote(reading, "citations: 2 resolved, 0 unresolved (0 missing, 0 ambiguous)");
    }

    private static string Gen(int i) => string.Create(CultureInfo.InvariantCulture, $"src/gen/F{i:D2}.cs");

    private static MapRecordInput Many(IEnumerable<string> paths) => new([new MapRepoFiles(_app, [.. paths], new Dictionary<string, string>(StringComparer.Ordinal))], _docGlobs, _names, RecordSamples.Now);

    [Fact, Trait("Lane", "unit")]
    public async Task The_cap_counts_touched_and_cited_files_together_so_a_record_over_it_gets_no_file_edge()
    {
        var all = Enumerable.Range(0, 70).Select(Gen).ToList();
        var pr = Tracker("github:pr/acme/app/11", string.Join(' ', all.Skip(40).Select(p => $"`{p}`")), RecordKind.Pr, _app, [.. all.Take(40).Select(p => new RecordLink("path:app/" + p, RecordLinkKind.Touches, Title: null))]);
        var reading = (await new MapRecordSources(await Store(pr), new FakeCodeSymbolSource([])).Read(Many(all), CancellationToken.None)).Value;

        Assert.Empty(reading.Map.Edges);
        AssertNote(reading, "records over the file cap: 1 (70 paths)");
        AssertNote(reading, "citations: 30 resolved, 0 unresolved (0 missing, 0 ambiguous)");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task One_relative_path_in_two_repos_is_two_files_for_the_cap()
    {
        var shared = Enumerable.Range(0, 26).Select(Gen).ToList();
        var links = shared.SelectMany(p => new[] { new RecordLink("path:app/" + p, RecordLinkKind.Touches, Title: null), new RecordLink("path:lib/" + p, RecordLinkKind.Touches, Title: null) });
        var pr = Tracker("github:pr/acme/app/12", "Rename", RecordKind.Pr, _app, [.. links]);
        var input = new MapRecordInput(
            [new MapRepoFiles(_app, shared, new Dictionary<string, string>(StringComparer.Ordinal)), new MapRepoFiles(_lib, shared, new Dictionary<string, string>(StringComparer.Ordinal))], _docGlobs, _names, RecordSamples.Now);
        var reading = (await new MapRecordSources(await Store(pr), new FakeCodeSymbolSource([])).Read(input, CancellationToken.None)).Value;

        Assert.Empty(reading.Map.Edges);
        AssertNote(reading, "records over the file cap: 1 (52 paths)");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_symbol_defined_at_the_same_relative_path_in_two_repos_is_ambiguous()
    {
        var store = await Store(Tracker("github:issue/acme/app/13", "The `Handler` is slow."));
        var symbols = new FakeCodeSymbolSource(
        [
            new FakeIndexedRepo(_app, CodeIndexState.Ok, "7c3e1a2f4b5d6e7081920a1b2c3d4e5f60718293", [Class("Handler", "src/common/Both.cs")], []),
            new FakeIndexedRepo(_lib, CodeIndexState.Ok, "7c3e1a2f4b5d6e7081920a1b2c3d4e5f60718294", [Class("Handler", "src/common/Both.cs")], []),
        ]);
        var reading = await Read(store, lib: true, symbols: symbols);

        Assert.Empty(reading.Map.Edges);
        AssertNote(reading, "citations: 0 resolved, 1 unresolved (0 missing, 1 ambiguous)");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_common_symbol_whose_first_hits_are_in_unmapped_files_still_finds_its_mapped_file()
    {
        var store = await Store(Doc("docs/guide.md", "b1", "The `Handler` does it."));
        var unmapped = Enumerable.Range(0, 12).Select(i => Class("Handler", string.Create(CultureInfo.InvariantCulture, $"a/vendor/H{i:D2}.cs")));
        var reading = await Read(store, symbols: Index([.. unmapped, Class("Handler", "src/billing/Tax.cs")]));

        Assert.Equal(["code:src/billing/Tax.cs"], Targets(reading, Doc1("docs/guide.md")), StringComparer.Ordinal);
        AssertNote(reading, "citations: 1 resolved, 0 unresolved (0 missing, 0 ambiguous)");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Two_link_kinds_to_the_same_record_are_one_edge_carrying_both_kinds_as_evidence()
    {
        var issue = Tracker("linear:issue/abc", "Tax is wrong");
        var pr = Tracker("github:pr/acme/app/9", "Fix tax", RecordKind.Pr, _app, new RecordLink("linear:issue/abc", RecordLinkKind.Parent, Title: null), new RecordLink("linear:issue/abc", RecordLinkKind.Relates, Title: null));
        var reading = await Read(await Store(issue, pr));

        var edge = Assert.Single(reading.Map.Edges, e => e.To.Value == "record:linear:issue/abc");
        Assert.Equal(["parent", "relates"], edge.EvidenceOrEmpty.Select(e => e.Span), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_source_stated_link_between_two_stored_records_is_a_mentions_edge_carrying_the_link_kind_and_makes_both_ends_nodes()
    {
        var issue = Tracker("linear:issue/abc", "Tax is wrong");
        var pr = Tracker("github:pr/acme/app/9", "Fix tax", RecordKind.Pr, _app, new RecordLink("linear:issue/abc", RecordLinkKind.Closes, Title: null), new RecordLink("linear:issue/missing", RecordLinkKind.Relates, Title: null));
        var reading = await Read(await Store(issue, pr));

        var edge = Assert.Single(reading.Map.Edges, e => e.To.Value == "record:linear:issue/abc");
        Assert.Equal(("record:github:pr/acme/app/9", EdgeType.Mentions, Provenance.Deterministic), (edge.From.Value, edge.Type, edge.Provenance));
        Assert.Equal("closes", Assert.Single(edge.EvidenceOrEmpty).Span);
        Assert.Contains(reading.Map.Nodes, n => n.Key.Value == "record:linear:issue/abc");
        Assert.DoesNotContain(reading.Map.Nodes, n => n.Key.Value.Contains("missing", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_removed_record_is_neither_a_node_nor_a_source_of_edges()
    {
        var store = await Store(Tracker("github:issue/acme/app/5", "cites `src/billing/Tax.cs`", repo: _app));
        _ = await store.Commit(new RecordBatch(RecordSamples.Github, [], [RecordKey.From("github:issue/acme/app/5")], new SyncPosition(SourceCursor.From("2"), SyncPhase.Incremental, BackfillCursor: null), RecordSamples.Now), CancellationToken.None);
        var reading = await Read(store);

        Assert.DoesNotContain(reading.Map.Nodes, n => n.Key.Value.Contains("issue/acme/app/5", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_build_names_the_store_stamp_it_read_and_reads_the_same_nodes_and_edges_twice()
    {
        var store = await Store(Doc("docs/guide.md", "b1", "see `src/billing/Tax.cs`"), Tracker("github:issue/acme/app/6", "see `src/billing/Invoice.cs`", repo: _app));
        var stamp = (await store.Stamps(CancellationToken.None)).Value.RecordStamp;
        var first = await Read(store);
        var second = await Read(store);

        Assert.Equal(stamp, first.Stamp);
        AssertNote(first, string.Create(CultureInfo.InvariantCulture, $"records: as of stamp {stamp}"));
        Assert.Equal(first.Map.Nodes.Select(n => n.Key.Value), second.Map.Nodes.Select(n => n.Key.Value), StringComparer.Ordinal);
        Assert.Equal(first.Map.Edges.Select(e => (e.From.Value, e.To.Value)), second.Map.Edges.Select(e => (e.From.Value, e.To.Value)));
    }
}
