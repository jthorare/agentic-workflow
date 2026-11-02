using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.BuildMap;

/// <summary>Map structure spec §4.3 and §4.4: a build measures the joined graph before it writes. Degraded keeps the draft with a note and a stored reason, refused writes nothing, 'context.checks: warn' turns a refusal into a note. Fixtures are built as graphs.</summary>
public sealed class BuildMapStructureChecksTests : IDisposable
{
    private readonly MapWorld _world = new();

    private static ContextScope Refusing => MapWorld.HumanWith(MapWorld.OnlyFixtureRepo);

    private static ContextScope Warning => MapWorld.HumanWith("""{"repos":["aw-split-panes"],"checks":{"mode":"warn"}}""");

    public void Dispose() => _world.Dispose();

    private async Task<int> SeedCurrent(GraphSnapshot graph) =>
        (await _world.Store.SaveCurrent(StructureGraphs.Import(graph), _world.Clock.GetUtcNow(), MapWorld.None)).Value.Number;

    private async Task<IReadOnlyList<MapVersion>> Drafts() => [.. (await _world.Store.Versions(MapWorld.None)).Value.Where(v => v.Status == MapVersionStatus.Draft)];

    [Fact, Trait("Lane", "unit")]
    public async Task A_map_over_the_degraded_limit_is_saved_with_a_note_and_a_stored_reason()
    {
        _ = await SeedCurrent(StructureGraphs.Isolated(total: 100, isolated: 41));

        var report = (await _world.Map.BuildMap(progress: null, Refusing, MapWorld.None)).Value;

        Assert.Contains("structure: 41% of features (a feature is a group of files) have no link to another feature or a table; limit 5%", report.Degraded, StringComparer.Ordinal);
        Assert.Equal("structure 41% isolated, limit 5%", report.Draft.Checks);
        Assert.Equal(report.Draft.Checks, (await Drafts()).Single().Checks);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_stored_reason_is_copied_on_promote_and_map_status_shows_it()
    {
        _ = await SeedCurrent(StructureGraphs.Isolated(total: 100, isolated: 41));
        var built = (await _world.Map.BuildMap(progress: null, Refusing, MapWorld.None)).Value;
        _ = await _world.Store.Promote(built.Draft.Number, MapWorld.None);

        var status = (await _world.Map.MapStatus(anchor: null, Refusing, MapWorld.None)).Value;

        Assert.Equal("structure 41% isolated, limit 5%", status.Checks);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_map_that_built_clean_stores_no_reason_and_the_report_carries_the_measures_anyway()
    {
        _ = await SeedCurrent(StructureGraphs.Isolated(total: 100, isolated: 3));

        var report = (await _world.Map.BuildMap(progress: null, Refusing, MapWorld.None)).Value;

        Assert.Null(report.Draft.Checks);
        Assert.DoesNotContain(report.Degraded, d => d.StartsWith("structure", StringComparison.Ordinal));
        Assert.Equal((3, 100), (report.Checks?.Isolated.Count, report.Checks?.Isolated.Total));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_map_over_the_refuse_limit_is_refused_and_no_draft_is_written()
    {
        var current = await SeedCurrent(StructureGraphs.Isolated(total: 100, isolated: 70));

        var built = await _world.Map.BuildMap(progress: null, Refusing, MapWorld.None);

        Assert.True(built.IsError);
        Assert.Equal("SND-CONTEXT-060", built.Error.Code);
        Assert.Contains("70% of features", built.Error.Message, StringComparison.Ordinal);
        Assert.Empty(await Drafts());
        Assert.Equal(current, (await _world.Store.Current(MapWorld.None)).Value?.Number);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_refused_build_leaves_the_draft_that_was_there()
    {
        _ = await SeedCurrent(StructureGraphs.Isolated(total: 100, isolated: 70));
        var earlier = (await _world.Map.BuildMap(progress: null, Warning, MapWorld.None)).Value.Draft.Number;

        var built = await _world.Map.BuildMap(progress: null, Refusing, MapWorld.None);

        Assert.Equal("SND-CONTEXT-060", built.Error.Code);
        Assert.Equal([earlier], (await Drafts()).Select(v => v.Number));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Too_many_features_over_the_cap_are_refused_with_the_size_code()
    {
        _ = await SeedCurrent(StructureGraphs.Oversized(total: 100, oversized: 11));

        var built = await _world.Map.BuildMap(progress: null, Refusing, MapWorld.None);

        Assert.Equal("SND-CONTEXT-061", built.Error.Code);
        Assert.Empty(await Drafts());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_checks_warn_a_refusal_saves_a_draft_and_a_note()
    {
        _ = await SeedCurrent(StructureGraphs.Isolated(total: 100, isolated: 70));

        var report = (await _world.Map.BuildMap(progress: null, Warning, MapWorld.None)).Value;

        Assert.Contains("structure: 70% of features (a feature is a group of files) have no link to another feature or a table; limit 60%", report.Degraded, StringComparer.Ordinal);
        Assert.Equal("structure 70% isolated, limit 60%", (await Drafts()).Single().Checks);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_map_with_no_file_edges_is_saved_with_the_no_file_edges_note()
    {
        _ = await SeedCurrent(StructureGraphs.Isolated(total: 40, isolated: 40, fileEdges: false));

        var report = (await _world.Map.BuildMap(progress: null, Refusing, MapWorld.None)).Value;

        Assert.Contains("structure: no file edges (imports and data stores not extracted)", report.Degraded, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_checks_key_takes_refuse_or_warn_and_nothing_else()
    {
        Assert.Equal(ChecksMode.Refuse, Read("{}").Value.Checks);
        Assert.Equal(ChecksMode.Refuse, Read("""{"checks":{"mode":"refuse"}}""").Value.Checks);
        Assert.Equal(ChecksMode.Warn, Read("""{"checks":{"mode":"warn"}}""").Value.Checks);
        var wrong = Read("""{"checks":{"mode":"off"}}""");
        Assert.Equal("SND-CONTEXT-001", wrong.Error.Code);
        Assert.Contains("context.checks.mode", wrong.Error.Message, StringComparison.Ordinal);
    }

    private static Kernel.Result<ContextSettings> Read(string json)
    {
        using var section = System.Text.Json.JsonDocument.Parse(json);
        return SettingsReader.Read(section.RootElement);
    }
}
