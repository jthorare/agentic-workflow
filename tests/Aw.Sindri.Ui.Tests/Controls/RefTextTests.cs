using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Rows;

namespace Aw.Sindri.Ui.Tests.Controls;

/// <summary>A ref in a row shortens in the middle and keeps its end (#2978 grammar): acme-platform/exports reads acme…/exports, never acme-platform/ex….</summary>
[Lane(TestLane.Unit)]
public sealed class RefTextTests
{
    [Fact]
    public void A_ref_that_fits_is_returned_as_it_is() =>
        Assert.Equal("acme-platform/exports", RefText.ShortenMiddle("acme-platform/exports", 21));

    [Fact]
    public void A_long_repo_ref_loses_the_middle_of_the_owner_and_keeps_the_repo()
    {
        var shortened = RefText.ShortenMiddle("acme-platform/exports", 14);

        Assert.True(shortened.Length <= 14, shortened);
        Assert.StartsWith("acme", shortened, StringComparison.Ordinal);
        Assert.Contains('…', shortened);
        Assert.EndsWith("/exports", shortened, StringComparison.Ordinal);
    }

    [Fact]
    public void A_branch_ref_keeps_its_last_characters()
    {
        var shortened = RefText.ShortenMiddle("feature/very-long-branch-name-4182", 20);

        Assert.True(shortened.Length <= 20, shortened);
        Assert.StartsWith("feature", shortened, StringComparison.Ordinal);
        Assert.Contains('…', shortened);
        Assert.EndsWith("4182", shortened, StringComparison.Ordinal);
    }

    [Fact]
    public void The_ellipsis_is_one_character_and_there_is_only_one()
    {
        var shortened = RefText.ShortenMiddle("a-very-long-owner-name/a-long-repo-name", 16);

        Assert.Equal(1, shortened.Count(c => c == '…'));
        Assert.True(shortened.Length <= 16, shortened);
    }
}
