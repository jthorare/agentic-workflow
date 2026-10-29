using Aw.Context.Core.Linear;

namespace Aw.Context.Tests.Linear;

[Trait("Lane", "unit")]
public sealed class LinearIdentifiersTests
{
    private static readonly HashSet<string> _teams = new(["SPL"], StringComparer.Ordinal);

    [Theory]
    [InlineData("https://linear.app/split-panes/issue/SPL-103/toggle-split-orientation", "SPL-103")]
    [InlineData("https://linear.app/fixture-labs/issue/SPL-103", "SPL-103")]
    public void FromPermalink_reads_the_identifier_with_or_without_a_slug(string url, string expected) =>
        Assert.Equal(expected, LinearIdentifiers.FromPermalink(url, _teams));

    [Theory]
    [InlineData("https://linear.app/w/issue/ENG-1/x")]
    [InlineData("https://example.com/w/issue/SPL-1/x")]
    [InlineData("http://linear.app/w/issue/SPL-1/x")]
    [InlineData("https://linear.app/w/issue/SPL-1a/x")]
    [InlineData("https://linear.app/w/issue/SPL-/x")]
    [InlineData("https://linear.app/w/project/SPL-1/x")]
    [InlineData("https://linear.app/w/issue")]
    [InlineData("not a url")]
    [InlineData(null)]
    public void FromPermalink_refuses_what_is_not_a_read_teams_issue_link(string? url) =>
        Assert.Null(LinearIdentifiers.FromPermalink(url, _teams));
}
