using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Symbols;
using Aw.Context.Features.BuildMap;
using Aw.Context.Testing.Records;
using Aw.Context.Testing.Symbols;

namespace Aw.Context.Tests.BuildMap;

/// <summary>Issue 1681: a cited path resolves by unique name or suffix, a deleted file is history, and env vars, rule ids and field names are not symbol citations.</summary>
public sealed class CitationResolutionTests
{
    private static readonly ContextRepoName _app = ContextRepoName.From("app");
    private static readonly string[] _docGlobs = ["docs/**/*.md"];
    private static readonly string[] _names = ["AGENTS.md"];
    private static readonly string[] _paths = ["docs/guide.md", "src/billing/Invoice.cs", "src/billing/Tax.cs", "src/ui/Tax.cs", "tests/Aw.App.Testing/Map/FakeStore.cs"];
    private static readonly string[] _removed = ["scorer/src/run.ts"];

    private static RecordWrite Doc(params string[] texts) =>
        RecordSamples.Write(RecordSamples.Record(RepoDocs.Key(_app, "docs/guide.md").Value, "b1", RepoDocs.Id) with { Kind = RecordKind.Doc, Repo = _app, Title = "docs/guide.md" }, texts);

    private static async Task<FakeRecordStore> Store(RecordWrite write)
    {
        var store = new FakeRecordStore();
        _ = await store.Commit(RecordSamples.Batch(write.Record.Source, 1, [write]), CancellationToken.None);
        return store;
    }

    private static MapRecordInput Input(bool removed = false) => new(
        [new MapRepoFiles(_app, _paths, new Dictionary<string, string>(StringComparer.Ordinal) { ["docs/guide.md"] = "b1" }) { Removed = removed ? _removed : [] }],
        _docGlobs, _names, RecordSamples.Now);

    private static FakeCodeSymbolSource Index(params SymbolSite[] symbols) =>
        new([new FakeIndexedRepo(_app, CodeIndexState.Ok, "7c3e1a2f4b5d6e7081920a1b2c3d4e5f60718293", symbols, [])]);

    private static SymbolSite Class(string name, string path) => new($"{path}#{name}", name, "class", path, 1, 40, Signature: null, Exported: true);

    private static async Task<MapRecordReading> Read(string text, bool removed = false, FakeCodeSymbolSource? symbols = null)
    {
        var result = await new MapRecordSources(await Store(Doc(text)), symbols ?? new FakeCodeSymbolSource([])).Read(Input(removed), CancellationToken.None);
        return result.Value;
    }

    private static void AssertNote(MapRecordReading reading, string note) => Assert.Contains(reading.Notes, n => string.Equals(n, note, StringComparison.Ordinal));

    private static List<string> Targets(MapRecordReading reading) =>
        [.. reading.Map.Edges.Where(e => e.Type == EdgeType.Mentions).Select(e => e.To.Value).Order(StringComparer.Ordinal)];

    [Fact, Trait("Lane", "unit")]
    public async Task A_bare_file_name_exactly_one_file_has_resolves_to_that_file()
    {
        var reading = await Read("See `Invoice.cs`.");

        Assert.Equal(["code:src/billing/Invoice.cs"], Targets(reading), StringComparer.Ordinal);
        AssertNote(reading, "citations: 1 resolved, 0 unresolved (0 missing, 0 ambiguous)");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_path_with_its_leading_directories_cut_off_resolves_when_exactly_one_file_ends_with_it_on_whole_segments()
    {
        var reading = await Read("See `billing/Invoice.cs`, `Aw.App.Testing/Map/FakeStore.cs` and `illing/Invoice.cs`.");

        Assert.Equal(["code:src/billing/Invoice.cs", "code:tests/Aw.App.Testing/Map/FakeStore.cs"], Targets(reading), StringComparer.Ordinal);
        AssertNote(reading, "citations: 2 resolved, 1 unresolved (1 missing, 0 ambiguous)");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_bare_name_or_suffix_several_files_have_is_ambiguous_and_makes_no_edge()
    {
        var reading = await Read("See `Tax.cs`.");

        Assert.Empty(Targets(reading));
        AssertNote(reading, "citations: 0 resolved, 1 unresolved (0 missing, 1 ambiguous)");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_cited_file_the_repo_has_since_deleted_is_history_not_missing()
    {
        var reading = await Read("See `scorer/src/run.ts` and `src/billing/Tax.cs`.", removed: true);

        AssertNote(reading, "citations: 1 resolved, 0 unresolved (0 missing, 0 ambiguous)");
        AssertNote(reading, "cited files since removed: 1");
        Assert.DoesNotContain(reading.Degraded, n => n.StartsWith("cited files since removed", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Env_vars_rule_ids_field_names_and_snake_case_names_the_index_has_no_definition_for_are_not_symbol_citations()
    {
        var reading = await Read("Set `AW_SINDRI_HOME`, see `RS0041`, read `reasonCode` and `asOf`, call `scope_read`, and `UnknownThing`.", symbols: Index(Class("Other", "src/billing/Tax.cs")));

        AssertNote(reading, "citations: 0 resolved, 1 unresolved (1 missing, 0 ambiguous)");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lower_camel_or_all_caps_name_the_index_defines_still_resolves()
    {
        var reading = await Read("See `reasonCode` and `MAX_ROWS`.", symbols: Index(Class("reasonCode", "src/billing/Tax.cs"), Class("MAX_ROWS", "src/billing/Invoice.cs")));

        Assert.Equal(["code:src/billing/Invoice.cs", "code:src/billing/Tax.cs"], Targets(reading), StringComparer.Ordinal);
        AssertNote(reading, "citations: 2 resolved, 0 unresolved (0 missing, 0 ambiguous)");
    }
}
