using System.Globalization;
using Aw.Context.Core.Results;
using Aw.Context.Features.QueryContext;

namespace Aw.Context.Tests.QueryContext;

public sealed class CodeRankerTests
{
    private static ResultItem Code(string path, int depth) =>
        new("code", "code:" + path, path, "implements feature:x", Score: null, ["implements→feature:x"], Provenance.Deterministic, Confirmed: false, Trust.Untrusted, ["C1"])
        { Depth = depth };

    private static List<ResultItem> Directory(int count) =>
        [.. Enumerable.Range(0, count).Select(i => Code("src/Other/File" + i.ToString("D3", CultureInfo.InvariantCulture) + ".cs", depth: 2))];

    [Fact, Trait("Lane", "unit")]
    public void Items_carry_scores_in_descending_order()
    {
        var page = CodeRanker.Rank([Code("src/A/Zeta.cs", 2), Code("src/Meta/LaneRecycleCommand.cs", 1), Code("src/B/Other.cs", 3)], "lane recycle", symbol: null, offset: 0, CodeRanker.DefaultLimit);

        Assert.All(page.Items, i => Assert.NotNull(i.Score));
        var scores = page.Items.Select(i => i.Score!.Value).ToList();
        Assert.Equal([.. scores.OrderDescending()], scores);
        Assert.Equal("code:src/Meta/LaneRecycleCommand.cs", page.Items[0].Node);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_closest_code_file_outranks_a_spec_with_the_same_words()
    {
        var page = CodeRanker.Rank([Code("docs/superpowers/specs/2026-10-01-lane-recycle-design.md", 1), Code("src/Meta/LaneRecycleCommand.cs", 0)], "lane recycle", symbol: null, offset: 0, CodeRanker.DefaultLimit);

        Assert.Equal("code:src/Meta/LaneRecycleCommand.cs", page.Items[0].Node);
    }

    [Fact, Trait("Lane", "unit")]
    public void Only_the_top_25_come_back_with_a_cursor_for_the_rest()
    {
        var page = CodeRanker.Rank(Directory(60), "other", symbol: null, offset: 0, CodeRanker.DefaultLimit);
        var next = CodeRanker.Rank(Directory(60), "other", symbol: null, offset: int.Parse(page.NextCursor ?? "0", CultureInfo.InvariantCulture), CodeRanker.DefaultLimit);

        Assert.Equal(25, page.Items.Count);
        Assert.Equal(60, page.Total);
        Assert.NotNull(page.NextCursor);
        Assert.Equal(25, next.Items.Count);
        Assert.Empty(page.Items.Select(i => i.Node).Intersect(next.Items.Select(i => i.Node), StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_last_page_has_no_cursor()
    {
        var page = CodeRanker.Rank(Directory(30), "other", symbol: null, offset: 25, CodeRanker.DefaultLimit);

        Assert.Equal(5, page.Items.Count);
        Assert.Null(page.NextCursor);
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_item_says_why_in_words_that_are_not_the_implements_line()
    {
        var page = CodeRanker.Rank([Code("src/Meta/LaneRecycleCommand.cs", 0), Code("src/B/Other.cs", 3)], "lane recycle", symbol: null, offset: 0, CodeRanker.DefaultLimit);

        Assert.All(page.Items, i =>
        {
            Assert.NotEmpty(i.Why);
            Assert.False(string.IsNullOrWhiteSpace(i.Why[0]));
            Assert.DoesNotContain("implements→", i.Why[0], StringComparison.Ordinal);
        });
    }
}
