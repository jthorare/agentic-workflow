using Aw.Context.Core.Graph;
using Aw.Context.Testing.Fixtures;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.BuildMap;

public sealed class BuildMapDataStoreTests
{
    private static Dictionary<string, string> Files()
    {
        var files = new Dictionary<string, string>(MapWorld.BuildFiles, StringComparer.Ordinal);
        foreach (var (path, text) in DataStoreRepo.Files)
        {
            files[path] = text;
        }

        return files;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_declared_tables_are_proposed_as_things_with_their_schema_files()
    {
        using var world = new MapWorld(new LeakyRepoReader(MapWorld.Head, Files()));

        var report = (await world.Map.BuildMap(progress: null, MapWorld.HumanWith(MapWorld.OnlyFixtureRepo), MapWorld.None)).Value;

        var things = report.Proposed.Where(p => p.Key.Kind == NodeKind.Thing).ToList();
        Assert.Equal(["thing:audit-event", "thing:invoice-line", "thing:shared-link"], things.Select(p => p.Key.Value), StringComparer.Ordinal);
        Assert.Equal(["code:db/schema/001_core.sql"], things.Single(p => p.Key.Value == "thing:audit-event").Files.Select(f => f.Value), StringComparer.Ordinal);
        Assert.Equal(["code:db/schema/002_billing.sql"], things.Single(p => p.Key.Value == "thing:invoice-line").Files.Select(f => f.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_saved_draft_holds_exactly_the_four_reads_and_writes_edges()
    {
        using var world = new MapWorld(new LeakyRepoReader(MapWorld.Head, Files()));

        _ = await world.Map.BuildMap(progress: null, MapWorld.HumanWith(MapWorld.OnlyFixtureRepo), MapWorld.None);

        var draft = (await world.Store.Snapshot(draft: true, MapWorld.None)).Value!;
        Assert.Equal(
            [
                "code:src/billing/lines.ts -Reads-> thing:invoice-line",
                "code:src/billing/lines.ts -Writes-> thing:invoice-line",
                "code:src/links/read.ts -Reads-> thing:shared-link",
                "code:src/links/write.ts -Writes-> thing:audit-event",
            ],
            draft.Graph.Edges.Where(e => e.Type is EdgeType.Reads or EdgeType.Writes && e.From.Kind == NodeKind.Code).Select(e => $"{e.From.Value} -{e.Type}-> {e.To.Value}").Order(StringComparer.Ordinal),
            StringComparer.Ordinal);
    }
}
