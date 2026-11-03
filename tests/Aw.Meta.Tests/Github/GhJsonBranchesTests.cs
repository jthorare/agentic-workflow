using Aw.Meta.Core.Github;
using Aw.Meta.Core.Watch;

namespace Aw.Meta.Tests.Github;

public sealed class GhJsonBranchesTests
{
    [Fact, Trait("Lane", "unit")]
    public void Pull_requests_are_read_with_their_head_branches()
    {
        const string json = """[{"number":7,"state":"MERGED","headRefName":"feat/x","headRefOid":"abc1234"},{"number":8,"state":"OPEN","headRefName":"fix/y","headRefOid":"def5678"},{"number":9,"state":"CLOSED","headRefName":"z","headRefOid":"0000000"}]""";

        var list = GhJson.Branches(json);

        Assert.Equal(
            [new PrBranch(7, PrState.Merged, "feat/x", "abc1234"), new PrBranch(8, PrState.Open, "fix/y", "def5678"), new PrBranch(9, PrState.Closed, "z", "0000000")],
            list);
    }

    [Fact, Trait("Lane", "unit")]
    public void Anything_that_is_not_a_list_is_nothing_and_entries_without_a_number_are_skipped()
    {
        Assert.Null(GhJson.Branches("{}"));
        Assert.Null(GhJson.Branches("not json"));
        Assert.Empty(GhJson.Branches("""[{"state":"OPEN"}]""")!);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_time_a_pull_request_ended_is_read_from_mergedAt_or_closedAt()
    {
        const string json = """[{"number":7,"state":"MERGED","headRefName":"a","headRefOid":"1","mergedAt":"2026-11-03T11:30:00Z","closedAt":"2026-11-03T11:30:00Z"},{"number":9,"state":"CLOSED","headRefName":"b","headRefOid":"2","mergedAt":null,"closedAt":"2026-11-03T12:00:00Z"},{"number":8,"state":"OPEN","headRefName":"c","headRefOid":"3","mergedAt":null,"closedAt":null}]""";

        var list = GhJson.Branches(json)!;

        Assert.Equal(
            [DateTimeOffset.Parse("2026-11-03T11:30:00Z", System.Globalization.CultureInfo.InvariantCulture), DateTimeOffset.Parse("2026-11-03T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture), null],
            list.Select(p => p.EndedAt));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData(",\"mergedAt\":null")]
    [InlineData(",\"mergedAt\":\"garbage\"")]
    [InlineData(",\"mergedAt\":12")]
    [InlineData(",\"mergedAt\":\"0001-01-01T00:00:00Z\"")]
    public void A_missing_null_or_unreadable_end_time_is_null(string ended)
    {
        var list = GhJson.Branches("[{\"number\":7,\"state\":\"MERGED\",\"headRefName\":\"a\",\"headRefOid\":\"1\"" + ended + "}]")!;

        Assert.Null(Assert.Single(list).EndedAt);
    }
}
