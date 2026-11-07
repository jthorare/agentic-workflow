using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Reviews;

namespace Aw.Sindri.Ui.Tests.ReviewPage;

/// <summary>The diff's keys, their scope and the focus rules around them (review page spec §6.4): j/k files, n/p hunks, o fold, Enter on a folded group, c a fix task from a line.</summary>
[Lane(TestLane.Unit)]
public sealed class ReviewPageKeysTests
{
    private const string X = "src/Api/A/A.Core/X.cs";

    private const string Retry = "src/Api/Migrations/0042_retry.sql";

    private readonly ReviewPageFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static bool Press(ReviewPageViewModel page, string key) => page.PressDiffKey(key, focusInDiff: true, textBoxHasFocus: false);

    private static ReviewFixRequestedEventArgs? Asked(ReviewPageViewModel page, Action act)
    {
        ReviewFixRequestedEventArgs? asked = null;
        page.FixRequested += (_, e) => asked = e;
        act();
        return asked;
    }

    [Fact]
    public async Task J_and_K_walk_the_files_of_the_open_groups_and_stop_at_both_ends()
    {
        var page = await _f.Opened(Token);
        var seen = new List<string?>();

        foreach (var key in (string[])["j", "j", "j", "j", "k", "k", "k", "k"])
        {
            Assert.True(Press(page, key));
            seen.Add(page.Diff.FocusedFile?.Path);
        }

        Assert.Equal([X, "src/Api/A/A.Core/Budget.cs", "src/Api/A/A.Core/Old.cs", Retry, "src/Api/A/A.Core/Old.cs", "src/Api/A/A.Core/Budget.cs", X, X], seen, StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_folded_groups_files_are_skipped_until_the_group_is_open()
    {
        var page = await _f.Opened(Token);
        for (var i = 0; i < 4; i++)
        {
            _ = Press(page, "j");
        }

        Assert.Equal(Retry, page.Diff.FocusedFile?.Path);   // the Assets group is folded: j stops at the last open file

        page.Diff.Groups[1].IsFolded = false;
        _ = Press(page, "j");

        Assert.Equal("assets/budget.png", page.Diff.FocusedFile?.Path);
    }

    [Fact]
    public async Task A_file_key_puts_focus_on_the_files_first_line()
    {
        var page = await _f.Opened(Token);

        _ = Press(page, "j");

        Assert.Equal("line 10: var a = 1;", page.Diff.FocusedLine?.AutomationName);
        Assert.Equal("@@ -10,3 +10,6 @@", page.Diff.FocusedHunk?.Header);
    }

    [Fact]
    public async Task N_and_P_walk_the_hunks_across_files_and_skip_a_file_with_no_hunk()
    {
        var page = await _f.Opened(Token);
        var seen = new List<(string?, string?)>();

        foreach (var key in (string[])["n", "n", "n", "p", "p"])
        {
            Assert.True(Press(page, key));
            seen.Add((page.Diff.FocusedFile?.Path, page.Diff.FocusedHunk?.Header));
        }

        // the deleted file shows no hunk until Show, so n goes past it to the migration, the last hunk there is
        Assert.Equal(
            [(X, "@@ -10,3 +10,6 @@"), ("src/Api/A/A.Core/Budget.cs", "@@ -1,2 +1,2 @@"), (Retry, "@@ -0,0 +1,2 @@"), ("src/Api/A/A.Core/Budget.cs", "@@ -1,2 +1,2 @@"), (X, "@@ -10,3 +10,6 @@")],
            seen);
    }

    [Fact]
    public async Task O_folds_the_focused_files_group_and_focus_goes_to_the_group_header()
    {
        var page = await _f.Opened(Token);
        _ = Press(page, "j");

        Assert.True(Press(page, "o"));

        Assert.True(page.Diff.Groups[0].IsFolded);
        Assert.Equal(new ReviewFocus(ReviewFocusRegion.GroupHeader, "Core"), page.Focus);
        Assert.Null(page.Diff.FocusedFile);   // its files left the list
    }

    [Fact]
    public async Task O_and_Enter_on_a_folded_groups_header_open_it_and_focus_stays_on_the_header()
    {
        var page = await _f.Opened(Token);
        _ = Press(page, "j");
        _ = Press(page, "o");

        Assert.True(Press(page, "Enter"));

        Assert.False(page.Diff.Groups[0].IsFolded);
        Assert.Equal(new ReviewFocus(ReviewFocusRegion.GroupHeader, "Core"), page.Focus);

        _ = Press(page, "o");   // o on a header folds it again
        Assert.True(page.Diff.Groups[0].IsFolded);
    }

    [Fact]
    public async Task O_acts_on_the_group_whose_header_really_has_focus_not_the_last_one_the_page_asked_for()
    {
        var page = await _f.Opened(Token);
        _ = Press(page, "j");   // the page asked for a line in Core

        page.FocusedFrom(ReviewFocusRegion.GroupHeader, "Assets");   // the owner clicked the Assets header
        Assert.True(page.Diff.Groups[1].IsFolded);
        Assert.True(Press(page, "o"));

        Assert.Equal((false, false), (page.Diff.Groups[0].IsFolded, page.Diff.Groups[1].IsFolded));   // Assets starts folded and opens; o on Core would have folded it (true, true)
    }

    [Fact]
    public async Task J_onto_a_file_with_no_lines_focuses_that_files_header_and_p_starts_from_it()
    {
        var page = await _f.Opened(Token);
        _ = Press(page, "j");
        _ = Press(page, "j");
        _ = Press(page, "j");   // Old.cs: deleted, its contents hidden

        Assert.Equal(new ReviewFocus(ReviewFocusRegion.FileNote, "src/Api/A/A.Core/Old.cs"), page.Focus);
        Assert.Equal("src/Api/A/A.Core/Old.cs", page.Diff.FocusedFile?.Path);

        _ = Press(page, "p");

        Assert.Equal("src/Api/A/A.Core/Budget.cs", page.Diff.FocusedFile?.Path);   // the hunk before Old.cs, not the diff's first
    }

    [Fact]
    public async Task Enter_does_nothing_when_no_group_header_has_focus()
    {
        var page = await _f.Opened(Token);
        _ = Press(page, "j");

        Assert.False(Press(page, "Enter"));
        Assert.False(page.Diff.Groups[0].IsFolded);
    }

    [Fact]
    public async Task C_on_the_focused_added_line_asks_for_a_fix_task_from_that_line_and_returns_to_it()
    {
        var page = await _f.Opened(Token);
        _ = Press(page, "j");
        page.Diff.FocusLine(page.Diff.FocusedFile!.Hunks[0].Lines[2]);   // "+var b = 3;", new line 11

        var asked = Asked(page, () => Assert.True(Press(page, "c")));

        Assert.Equal(new ReviewFixSource(ReviewFixKind.Line, "var b = 3;", string.Empty, string.Empty, X, Line: 11, Added: true), asked?.Source);
        Assert.Equal(new ReviewFocus(ReviewFocusRegion.DiffLine, X + ":+11"), asked?.ReturnTo);
        Assert.Equal(new ReviewFixTarget("acme/api", 2071, "a1b2c3d4e5f60718293a4b5c6d7e8f9012345678", "a1b2c3d", "feat/retry-budget", "sam-dev", PostsThreads: false, "sindri-pr-review"), asked?.Target);
    }

    [Fact]
    public async Task C_on_a_removed_line_asks_for_a_removed_line_fix_with_its_old_line_number()
    {
        var page = await _f.Opened(Token);
        _ = Press(page, "j");
        page.Diff.FocusLine(page.Diff.FocusedFile!.Hunks[0].Lines[1]);   // "-var b = 2;", old line 11

        var asked = Asked(page, () => Assert.True(Press(page, "c")));

        Assert.Equal(new ReviewFixSource(ReviewFixKind.Line, "var b = 2;", string.Empty, string.Empty, X, Line: 11, Added: false, Removed: true), asked?.Source);
        Assert.Equal(new ReviewFocus(ReviewFocusRegion.DiffLine, X + ":-11"), asked?.ReturnTo);
    }

    [Fact]
    public async Task C_with_no_line_focused_asks_for_nothing()
    {
        var page = await _f.Opened(Token);

        var asked = Asked(page, () => Assert.False(Press(page, "c")));

        Assert.Null(asked);
    }

    [Fact]
    public async Task Keys_do_nothing_when_focus_is_outside_the_diff_or_a_text_box_has_it()
    {
        var page = await _f.Opened(Token);

        Assert.False(page.PressDiffKey("j", focusInDiff: false, textBoxHasFocus: false));
        Assert.False(page.PressDiffKey("j", focusInDiff: true, textBoxHasFocus: true));
        Assert.Null(page.Diff.FocusedFile);

        Assert.True(page.PressDiffKey("j", focusInDiff: true, textBoxHasFocus: false));
        Assert.Equal(X, page.Diff.FocusedFile?.Path);
    }

    [Fact]
    public async Task A_key_that_is_not_the_pages_is_not_handled()
    {
        var page = await _f.Opened(Token);

        Assert.False(Press(page, "x"));
        Assert.False(Press(page, "Tab"));
    }

    [Fact]
    public async Task Send_as_fix_task_on_a_failed_check_asks_with_the_check_and_returns_to_its_row()
    {
        var page = await _f.Opened(Token);

        var asked = Asked(page, () => page.RequestCheckFix("ci-ui"));

        Assert.Equal(new ReviewFixSource(ReviewFixKind.Check, "Recipe change shows Ui tests ran", "ci-ui", "ui-tests: failure on a1b2c3d", string.Empty, Line: null, Added: false), asked?.Source);
        Assert.Equal(new ReviewFocus(ReviewFocusRegion.Panel, "requiredChecks:ci-ui"), asked?.ReturnTo);
    }

    [Fact]
    public async Task A_check_that_did_not_fail_asks_for_nothing()
    {
        var page = await _f.Opened(Token);

        Assert.Null(Asked(page, () => page.RequestCheckFix("lint")));
    }

    [Fact]
    public async Task Send_as_fix_task_on_an_unclaimed_requirement_asks_with_it_and_a_claimed_one_does_not()
    {
        var page = await _f.Opened(Token);

        var asked = Asked(page, () => page.RequestRequirementFix("REQ-E4"));
        var claimed = Asked(page, () => page.RequestRequirementFix("REQ-E3"));

        Assert.Equal(new ReviewFixSource(ReviewFixKind.Requirement, "Offline PRs answer from the cache", "REQ-E4", string.Empty, string.Empty, Line: null, Added: false), asked?.Source);
        Assert.Equal(new ReviewFocus(ReviewFocusRegion.Panel, "docMap:REQ-E4"), asked?.ReturnTo);
        Assert.Null(claimed);
    }

    [Fact]
    public async Task When_the_fix_sheet_closes_focus_goes_back_to_the_row_it_came_from()
    {
        var page = await _f.Opened(Token);
        var row = new ReviewFocus(ReviewFocusRegion.Panel, "requiredChecks:ci-ui");

        page.ReturnFromFix(row);

        Assert.Equal(row, page.Focus);
    }

    [Fact]
    public async Task When_the_source_row_is_gone_after_a_reload_focus_lands_on_the_strips_first_part()
    {
        var page = await _f.Opened(Token);
        var json = ReviewPageFixture.Golden("full");
        json["panels"]![0]!["data"]!["checks"]!.AsArray().RemoveAt(0);   // the failed check is gone
        _f.Serve(json);
        await page.Reload(Token);

        page.ReturnFromFix(new ReviewFocus(ReviewFocusRegion.Panel, "requiredChecks:ci-ui"));

        Assert.Equal(new ReviewFocus(ReviewFocusRegion.StripFirst), page.Focus);
    }
}
