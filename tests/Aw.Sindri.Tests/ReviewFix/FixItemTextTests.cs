using Aw.Kernel.Testing;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Features.SendReviewFix;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ReviewFix;

/// <summary>The words of a fix task's item and thread (PR review page spec §8.2 steps 1 and 2).</summary>
[Lane(TestLane.Unit)]
public sealed class FixItemTextTests
{
    private static readonly string _sha = new('a', 40);

    private static SendReviewFixRequest Line(string? side = null, string text = "Guard the null.\nIt crashes on retry.") =>
        new("acme", "api", 2071, "line", text, _sha, Source: null, "src/Sync.cs", 42, side, Item: null);

    [Fact]
    public void The_title_is_Fix_and_the_first_line()
        => Expect.Equal("Fix: Guard the null.", FixItemText.Title("Guard the null.\nIt crashes on retry."));

    [Fact]
    public void A_long_first_line_is_cut_to_200_characters_with_the_prefix()
        => Expect.Equal(200, FixItemText.Title(new string('x', 500)).Length);

    [Fact]
    public void The_body_quotes_the_finding_and_names_the_file_head_pr_and_branch()
    {
        var body = FixItemText.Body(Line(), "acme/api", "https://github.com/acme/api/pull/2071", "sam-dev/api:feat/retry");

        Assert.StartsWith("> Guard the null.\n> It crashes on retry.\n\n", body, StringComparison.Ordinal);
        Assert.Contains("file: src/Sync.cs:42\n", body, StringComparison.Ordinal);
        Assert.Contains($"head: {_sha}\n", body, StringComparison.Ordinal);
        Assert.Contains("pr: acme/api#2071\n", body, StringComparison.Ordinal);
        Assert.EndsWith("branch: sam-dev/api:feat/retry", body, StringComparison.Ordinal);
    }

    [Fact]
    public void A_line_on_the_new_side_is_one_line_comment_with_an_empty_review_body_and_the_marker()
    {
        var review = FixItemText.Review(Line(), "https://github.com/acme/api/pull/2071", "ITM-1");

        Expect.Equal(string.Empty, review.Body);
        Expect.Equal(PullReviewVerdict.Comment, review.Verdict);
        Expect.Equal("sindri.fix:ITM-1", review.Marker);
        Expect.Equal(new PullReviewComment("src/Sync.cs", 42, "Guard the null.\nIt crashes on retry."), Assert.Single(review.Comments));
    }

    [Fact]
    public void A_line_on_the_old_side_is_a_body_comment_naming_the_removed_line()
    {
        var review = FixItemText.Review(Line("old"), "https://github.com/acme/api/pull/2071", "ITM-1");

        Assert.Empty(review.Comments);
        Assert.StartsWith("src/Sync.cs:42 (removed line)\n\n", review.Body, StringComparison.Ordinal);
        Expect.Equal("sindri.fix:ITM-1", review.Marker);
    }
}
