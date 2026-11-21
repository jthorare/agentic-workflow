using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Features.HomeLanes;

namespace Aw.Sindri.Tests.HomeLanes;

/// <summary>The three aw-meta outputs read into a snapshot: the real field names, case-insensitively, and SND-LANES-001 for anything that is not the JSON it should be.</summary>
[Lane(TestLane.Unit)]
public sealed class LaneParserTests
{
    [Fact]
    public void The_merge_queue_the_lanes_and_the_usage_subjects_are_read_from_the_real_field_names()
    {
        var parsed = LaneParser.Parse(LaneFixtures.Status, LaneFixtures.ContextRecycle, LaneFixtures.UsageLimited);

        Assert.True(parsed.IsSuccess);
        Assert.Equal([(2897, "sindri-v3-ui", null), (2950, "sindri-v3-ui", null), (3214, "sindri-v3-api", "queued 2")], parsed.Value.Queue.Select(static q => (q.Pr, q.From, q.Github)));
        Assert.Equal([("meta", 146236L, 200000L, "Ok"), ("api", 212004L, 200000L, "Recycle")], parsed.Value.Lanes.Select(static l => (l.Lane, l.Tokens, l.Threshold, l.Verdict)));
        var limited = Assert.Single(parsed.Value.Usage, static u => string.Equals(u.State, "Limited", StringComparison.Ordinal));
        Assert.Equal(("engine", "Session", DateTimeOffset.Parse("2026-11-21T23:00:00+00:00", System.Globalization.CultureInfo.InvariantCulture)), (limited.Subject, limited.Kind, limited.ResetsAt));
    }

    [Fact]
    public void An_empty_queue_and_no_lanes_parse_to_empty_lists()
    {
        var parsed = LaneParser.Parse(LaneFixtures.StatusEmpty, """{"recycleAt":300000,"lanes":[],"attention":false}""", """{"subjects":[],"attention":false}""");

        Assert.True(parsed.IsSuccess);
        Assert.Empty(parsed.Value.Queue);
        Assert.Empty(parsed.Value.Lanes);
        Assert.Empty(parsed.Value.Usage);
    }

    [Theory]
    [InlineData("not json", LaneFixtures.ContextOk, LaneFixtures.UsageOk)]
    [InlineData(LaneFixtures.StatusEmpty, "", LaneFixtures.UsageOk)]
    [InlineData(LaneFixtures.StatusEmpty, LaneFixtures.ContextOk, "[1,2]")]
    [InlineData("""{"obligations":[]}""", LaneFixtures.ContextOk, LaneFixtures.UsageOk)]
    public void Output_that_is_not_the_expected_json_is_SND_LANES_001(string status, string context, string usage)
    {
        var parsed = LaneParser.Parse(status, context, usage);

        Assert.True(parsed.IsError);
        Assert.Equal(SindriErrors.LanesUnavailable.Code, parsed.Error.Code);
    }
}
