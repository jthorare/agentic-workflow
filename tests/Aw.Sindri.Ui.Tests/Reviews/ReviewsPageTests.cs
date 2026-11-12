using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels;
using static Aw.Sindri.Ui.Tests.Reviews.ReviewsFixture;

namespace Aw.Sindri.Ui.Tests.Reviews;

/// <summary>The Reviews list (review page spec §6.1, §6.3, §16): the two groups, the sections, a row's columns, the header and every list state.</summary>
[Lane(TestLane.Unit)]
public sealed class ReviewsPageTests
{
    private readonly ReviewsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_page_is_loading_until_its_first_answer()
    {
        var page = _f.Page();
        page.SetShown(shown: true);
        Assert.True(page.IsLoading);

        await page.Refresh(Url, Token);

        Assert.False(page.IsLoading);
        Assert.Equal("Reviews", page.Title);
    }

    [Fact]
    public async Task Waiting_on_you_comes_first_then_yours_each_with_its_count()
    {
        var page = await _f.Shown(Token);

        Assert.Equal([("Waiting on you", 2, true), ("Yours", 2, false)], page.Groups.Select(g => (g.Title, g.Count, g.IsWaiting)));
        Assert.Equal(2, page.WaitingCount);
    }

    [Fact]
    public async Task A_project_section_names_its_kind_and_a_repo_section_does_not()
    {
        var page = await _f.Shown(Token);

        var waiting = page.Groups[0].Sections;
        Assert.Equal([("Sindri", "project"), ("acme/web", null)], waiting.Select(s => (s.Name, s.Kind)));
    }

    [Fact]
    public async Task A_waiting_row_shows_its_columns_and_reads_them_in_one_name()
    {
        var page = await _f.Shown(Token);

        var row = page.Groups[0].Sections[0].Rows[0];
        Assert.Equal(
            ("Add retry budget to the sync worker", "acme/api#2071", "sam-dev", "2 d", "failing", StateGlyph.Down, "2 open", "claims a doc", "review requested", false),
            (row.Title, row.Ref, row.Author, row.Age, row.Ci, row.CiGlyph, row.Required, row.Doc, row.WaitingChip, row.IsDraft));
        Assert.Equal("Add retry budget to the sync worker, acme/api#2071, review requested, by sam-dev, 2 d, CI failing, required checks 2 open, claims a doc", row.AutomationName);
    }

    [Fact]
    public async Task The_owners_own_rows_say_you_and_a_draft_has_its_chip()
    {
        var page = await _f.Shown(Token);

        var yours = page.Groups[1].Sections;
        Assert.Equal(("you", "running", StateGlyph.Stale, "1 open", null), (yours[0].Rows[0].Author, yours[0].Rows[0].Ci, yours[0].Rows[0].CiGlyph, yours[0].Rows[0].Required, yours[0].Rows[0].WaitingChip));
        var draft = yours[1].Rows[0];
        Assert.Equal((true, "no CI", string.Empty, "no doc named"), (draft.IsDraft, draft.Ci, draft.Required, draft.Doc));
    }

    [Fact]
    public async Task New_commits_since_the_owners_review_read_as_served()
    {
        var page = await _f.Shown(Token);

        var row = page.Groups[0].Sections[1].Rows[0];
        Assert.Equal(("reviewed, 2 new commits", "passing", StateGlyph.Ok, "none open"), (row.WaitingChip, row.Ci, row.CiGlyph, row.Required));
    }

    [Fact]
    public async Task The_header_reads_the_freshness_the_login_and_the_counts()
    {
        var page = await _f.Shown(Token);

        Assert.Equal("GitHub synced 14 min ago · reviews as joi-example · 4 open PRs in 2 repos", page.SubLine);
        Assert.Equal(("Library: 41 docs, 3 with errors", true), (page.LibraryText, page.LibraryWarns));
        Assert.Null(page.Notice);
        Assert.Null(page.StaleText);
        Assert.False(page.IsLoginPrompt);
        Assert.Null(page.EmptyLines);
    }

    [Fact]
    public async Task A_library_folder_that_cant_be_read_warns_in_the_header()
    {
        _f.Serve("library-unreadable");

        var page = await _f.Shown(Token);

        Assert.Equal(("Library: 33 docs, 1 folder can't be read", true), (page.LibraryText, page.LibraryWarns));
    }

    [Fact]
    public async Task A_clean_library_does_not_warn()
    {
        var list = Golden("some");
        list["library"]!["withErrors"] = 0;
        list["library"]!["text"] = "Library: 41 docs";
        _f.Serve(list);

        var page = await _f.Shown(Token);

        Assert.Equal(("Library: 41 docs", false), (page.LibraryText, page.LibraryWarns));
    }

    [Fact]
    public async Task No_login_shows_the_one_field_prompt_filled_with_the_suggestion()
    {
        _f.Serve("no-login");

        var page = await _f.Shown(Token);

        Assert.Equal((true, "Set your GitHub login", "joi-example"), (page.IsLoginPrompt, page.LoginHeading, page.LoginField));
        Assert.Empty(page.Groups);
        Assert.Equal(0, page.WaitingCount);
    }

    [Fact]
    public async Task No_rows_shows_the_three_empty_lines()
    {
        _f.Serve("empty");

        var page = await _f.Shown(Token);

        Assert.Equal(["Nothing waiting on you and no open PRs of yours.", "Repos are set in Settings → Connections.", "Reviews asked of a team don't show here yet."], page.EmptyLines, StringComparer.Ordinal);
        Assert.Empty(page.Groups);
    }

    [Fact]
    public async Task A_failing_source_keeps_the_last_rows_under_the_stale_line()
    {
        _f.Serve("github-failing");

        var page = await _f.Shown(Token);

        Assert.Equal("PR list is from 23:45: github failed: SND-INDEX-401", page.StaleText);
        Assert.Equal(2, page.Groups.Count);
        Assert.StartsWith("GitHub failing: SND-INDEX-401", page.SubLine, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Before_requested_reviewers_are_read_the_notice_shows()
    {
        _f.Serve("before-reviewers");

        var page = await _f.Shown(Token);

        Assert.Equal("Review requests show here once Sindri reads requested reviewers.", page.Notice);
    }
}
