using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Reviews;

namespace Aw.Sindri.Ui.Tests.ReviewPage;

/// <summary>The diff (review page spec §6.2, §6.3, §6.4, §16 "Wording the mockups set"): groups, folded groups, renamed, deleted, binary and too-large files, the cap, and the lines' names.</summary>
[Lane(TestLane.Unit)]
public sealed class ReviewPageDiffTests
{
    private readonly ReviewPageFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ReviewDiffFileViewModel File(ReviewPageViewModel page, string path) =>
        page.Diff.Groups.SelectMany(g => g.Files).Single(f => string.Equals(f.Path, path, StringComparison.Ordinal));

    [Fact]
    public async Task The_diff_is_ready_with_its_groups_a_folded_one_and_the_file_count()
    {
        var page = await _f.Opened(Token);

        Assert.Equal((ReviewPanelState.Ready, "5 files"), (page.Diff.State, page.Diff.CountText));
        Assert.Equal([("Core", false, 4), ("Assets", true, 1)], page.Diff.Groups.Select(g => (g.Name, g.IsFolded, g.FileCount)));
    }

    [Fact]
    public async Task A_group_reads_as_its_name_and_count_and_a_folded_one_says_collapsed()
    {
        var page = await _f.Opened(Token);

        Assert.Equal(["Core, 4 files", "Assets, collapsed, 1 file"], page.Diff.Groups.Select(g => g.AutomationName), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_modified_file_has_its_status_its_stat_and_a_hunk_whose_range_reads_lines_10_to_15()
    {
        var page = await _f.Opened(Token);

        var file = File(page, "src/Api/A/A.Core/X.cs");
        Assert.Equal(("modified", "+4 −1", string.Empty), (file.Status, file.Stat, file.Note));
        var hunk = Assert.Single(file.Hunks);
        Assert.Equal(("@@ -10,3 +10,6 @@", "lines 10 to 15"), (hunk.Header, hunk.AutomationName));
    }

    [Fact]
    public async Task A_hunks_lines_carry_their_kind_numbers_signs_and_names()
    {
        var page = await _f.Opened(Token);

        var lines = File(page, "src/Api/A/A.Core/X.cs").Hunks[0].Lines;
        Assert.Equal(
            [ReviewDiffLineKind.Context, ReviewDiffLineKind.Removed, ReviewDiffLineKind.Added, ReviewDiffLineKind.Added, ReviewDiffLineKind.Added, ReviewDiffLineKind.Added],
            lines.Select(l => l.Kind));
        Assert.Equal([(10, 10), (11, null), (null, 11), (null, 12), (null, 13), (null, 14)], lines.Select(l => (l.OldNumber, l.NewNumber)));
        Assert.Equal(["", "-", "+", "+", "+", "+"], lines.Select(l => l.Sign), StringComparer.Ordinal);
        Assert.Equal(
            ["line 10: var a = 1;", "line 11, removed: var b = 2;", "line 11, added: var b = 3;", "line 12, added: var c = 4;", "line 13, added: var d = 5;", "line 14, added: var e = 6;"],
            lines.Select(l => l.AutomationName),
            StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_renamed_file_says_where_from_and_how_much_is_the_same_and_still_shows_its_patch()
    {
        var page = await _f.Opened(Token);

        var file = File(page, "src/Api/A/A.Core/Budget.cs");
        Assert.Equal(("renamed", "Renamed from src/Api/A/A.Core/Limit.cs, 91% the same"), (file.Status, file.Note));
        _ = Assert.Single(file.Hunks);
    }

    [Fact]
    public async Task A_deleted_file_holds_its_contents_back_until_Show()
    {
        var page = await _f.Opened(Token);
        var file = File(page, "src/Api/A/A.Core/Old.cs");

        Assert.Equal(("Deleted file: 61 lines removed. Contents hidden.", true, true, 0), (file.Note, file.IsContentsHidden, file.HasShow, file.Hunks.Count));

        file.Show();

        Assert.Equal((false, 1), (file.IsContentsHidden, file.Hunks.Count));
    }

    [Fact]
    public async Task A_binary_file_says_so_and_offers_Open_on_GitHub_instead_of_a_patch()
    {
        var page = await _f.Opened(Token);

        var file = File(page, "assets/budget.png");
        Assert.Equal(("Binary file, not shown here.", true, false, 0, "https://github.com/acme/api/pull/2071/files#diff-2bec3859d529737e3b3b4a89205ed35e5db67851c7ee4c723c9aa102b9b02854"), (file.Note, file.HasOpenOnGitHub, file.HasShow, file.Hunks.Count, file.Url));
    }

    [Fact]
    public async Task At_GitHubs_cap_the_header_says_3000_files_and_a_too_large_file_links_to_GitHub()
    {
        _f.Serve("capped");

        var page = await _f.Opened(Token);

        Assert.Equal(("3,000 files", "GitHub lists the first 3,000 files; open the rest on GitHub"), (page.Diff.CountText, page.Diff.CappedText));
        var file = page.Diff.Groups.SelectMany(g => g.Files).Single(f => f.Note == "Too large to show here");
        Assert.Equal(("Too large to show here", true, 0), (file.Note, file.HasOpenOnGitHub, file.Hunks.Count));
    }

    [Fact]
    public async Task A_PR_with_no_files_says_so()
    {
        _f.Serve("empty-panels");

        var page = await _f.Opened(Token);

        Assert.Equal((ReviewPanelState.Empty, "This PR changes no files"), (page.Diff.State, page.Diff.Text));
        Assert.Empty(page.Diff.Groups);
    }

    [Fact]
    public async Task A_diff_the_forge_could_not_read_shows_its_error_words_while_the_panels_still_show()
    {
        _f.Serve("panel-errors");

        var page = await _f.Opened(Token);

        Assert.Equal((ReviewPanelState.Error, "Couldn't read the diff: github returned 502"), (page.Diff.State, page.Diff.Text));
        Assert.NotEmpty(page.Panels);
    }
}
