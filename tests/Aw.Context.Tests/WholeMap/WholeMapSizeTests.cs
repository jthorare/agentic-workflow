using System.Diagnostics;
using System.Globalization;
using Aw.Context.Core.WholeMap;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.WholeMap;

/// <summary>The cost of the answer at the budget (Part D, §2.3): 500 nodes, about 700 edges, cold then served from memory.</summary>
public sealed class WholeMapSizeTests
{
    private static string Id(int i) => i.ToString(CultureInfo.InvariantCulture);

    private static string FiveHundredNodes()
    {
        var lines = new List<string> { "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:" };
        var edges = new List<string> { "edges:" };
        for (var f = 0; f < 120; f++)
        {
            lines.Add($"  - {{key: \"feature:f{Id(f)}\", label: \"Feature {Id(f)}\"}}");
            if (f >= 40)
            {
                edges.Add($"  - {{from: \"feature:f{Id(f)}\", to: \"feature:f{Id(f % 40)}\", type: part-of}}");
            }
        }

        for (var s = 0; s < 120; s++)
        {
            lines.Add($"  - {{key: \"surface:screen/s{Id(s)}\", label: \"Screen {Id(s)}\"}}");
            edges.Add($"  - {{from: \"surface:screen/s{Id(s)}\", to: \"feature:f{Id(s)}\", type: part-of}}");
        }

        for (var a = 0; a < 100; a++)
        {
            lines.Add($"  - {{key: \"action:a{Id(a)}\", label: \"Action {Id(a)}\"}}");
            edges.Add($"  - {{from: \"action:a{Id(a)}\", to: \"surface:screen/s{Id(a)}\", type: on}}");
        }

        for (var c = 0; c < 160; c++)
        {
            lines.Add($"  - {{key: \"code:src/c{Id(c)}.ts\", label: \"c{Id(c)}\"}}");
            edges.Add($"  - {{from: \"code:src/c{Id(c)}.ts\", to: \"surface:screen/s{Id(c % 120)}\", type: implements}}");
            edges.Add($"  - {{from: \"code:src/c{Id(c)}.ts\", to: \"feature:f{Id(c % 120)}\", type: tests}}");
        }

        return MapWorld.Yaml([.. lines, .. edges]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_map_at_the_budget_is_answered_cold_in_well_under_a_second_and_warm_in_a_blink()
    {
        using var world = new MapWorld();
        _ = await world.ImportYaml(FiveHundredNodes());
        var query = new WholeMapQuery(Detail: null, [], Context.Core.Map.MapRead.Current);

        var cold = Stopwatch.StartNew();
        var first = (await world.Engine.WholeMap(query, MapWorld.Human, MapWorld.None)).Value;
        cold.Stop();
        var warm = Stopwatch.StartNew();
        _ = await world.Engine.WholeMap(query, MapWorld.Human, MapWorld.None);
        warm.Stop();

        Assert.Equal((WholeMapDetail.Nodes, 500, 620), (first.Detail, first.Counts.Nodes, first.Counts.Edges));
        Assert.InRange(cold.ElapsedMilliseconds, 0, 1000);   // measured about 90 ms on the owner's box, with the fakes
        Assert.InRange(warm.ElapsedMilliseconds, 0, 50);   // measured under 1 ms: the same graph object, the same question
    }
}
