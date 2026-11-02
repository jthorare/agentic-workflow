using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Features.Scoping;

namespace Aw.Sindri.Tests.Scoping.GapSearch;

/// <summary>'step.scope.reanchor' (issue 966, spec 4.6): the anchors a confirmed gap names come first after the code anchors already held, ahead of the map's own keys.</summary>
[Lane(TestLane.Unit)]
public sealed class ReanchorGapAnchorsTests
{
    private const string Map = """{"surfaces":[{"id":"S1","key":"feature:links","title":"Links"},{"id":"S2","key":"code:src/map.ts","title":"Map file"},{"id":"S3","key":"surface:route/open","title":"Open"}]}""";

    private static string[] Anchors(Dictionary<string, string> facts)
    {
        var ran = DeskBlocks.Run("step.scope.reanchor", new ScopeFacts(facts));
        Assert.True(ran.IsSuccess);
        return [.. JsonNode.Parse(ran.Value.Outputs["anchors"])!.AsArray().Select(static a => a!.GetValue<string>())];
    }

    [Fact]
    public void Gap_anchors_come_first_after_the_held_code_anchors_and_ahead_of_the_map_keys()
    {
        var facts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["scopeMap"] = Map,
            ["anchors"] = """["code:src/held.ts","feature:old"]""",
            ["gapAnchors"] = """["code:src/jobs/purge.ts","feature:billing"]""",
        };

        Assert.Equal(["code:src/held.ts", "code:src/jobs/purge.ts", "feature:billing", "feature:links", "surface:route/open", "code:src/map.ts"], Anchors(facts), StringComparer.Ordinal);
    }

    [Fact]
    public void Without_the_fact_the_result_is_what_it_was()
    {
        var facts = new Dictionary<string, string>(StringComparer.Ordinal) { ["scopeMap"] = Map, ["anchors"] = """["code:src/held.ts","feature:old"]""" };

        Assert.Equal(["code:src/held.ts", "feature:links", "surface:route/open", "code:src/map.ts"], Anchors(facts), StringComparer.Ordinal);
    }

    [Fact]
    public void Empty_gap_anchors_change_nothing()
    {
        var facts = new Dictionary<string, string>(StringComparer.Ordinal) { ["scopeMap"] = Map, ["anchors"] = """["code:src/held.ts"]""", ["gapAnchors"] = "[]" };

        Assert.Equal(["code:src/held.ts", "feature:links", "surface:route/open", "code:src/map.ts"], Anchors(facts), StringComparer.Ordinal);
    }

    [Fact]
    public void A_gap_anchor_the_map_already_has_is_listed_once()
    {
        var facts = new Dictionary<string, string>(StringComparer.Ordinal) { ["scopeMap"] = Map, ["anchors"] = "[]", ["gapAnchors"] = """["feature:links"]""" };

        Assert.Equal(["feature:links", "surface:route/open", "code:src/map.ts"], Anchors(facts), StringComparer.Ordinal);
    }
}
