using Aw.Kernel.Testing;
using Aw.Sindri.Core.Reviews;
using static Aw.Sindri.Tests.Reviews.ReviewPulls;

namespace Aw.Sindri.Tests.Reviews;

/// <summary>Who waits on the owner and how the Reviews list groups its rows (PR review page spec §5.6, §6.1).</summary>
[Lane(TestLane.Unit)]
public sealed class ReviewListBuilderTests
{
    private const string Login = "joi-example";

    private static ReviewListParts Build(IReadOnlyList<ReviewPull> pulls, Func<string, ReviewProjectName?>? projectOf = null) =>
        ReviewListBuilder.Build(Login, pulls, static _ => null, projectOf ?? (static _ => null));

    [Fact]
    public void A_requested_review_waits_on_the_owner_whatever_the_case_of_the_login()
    {
        var wait = ReviewListBuilder.Classify(Login, Pull(requested: ["JOI-Example"]));

        Assert.Equal(((ReviewWaitingKind?)ReviewWaitingKind.Requested, "review requested", null), (wait?.Kind, wait?.Text, wait?.NewCommits));
    }

    [Fact]
    public void A_review_on_an_older_commit_waits_with_the_count_of_new_commits()
    {
        var wait = ReviewListBuilder.Classify(Login, Pull(reviews: [new ReviewedAt("Joi-Example", Old, NewCommits: 2)]));

        Assert.Equal(((ReviewWaitingKind?)ReviewWaitingKind.NewCommits, "reviewed, 2 new commits", 2), (wait?.Kind, wait?.Text, wait?.NewCommits));
    }

    [Fact]
    public void A_review_on_an_older_commit_with_an_unknown_count_says_new_commits_without_a_number()
    {
        var wait = ReviewListBuilder.Classify(Login, Pull(reviews: [new ReviewedAt(Login, Old, NewCommits: null)]));

        Assert.Equal(("reviewed, new commits", null), (wait?.Text, wait?.NewCommits));
    }

    [Fact]
    public void The_owners_own_PR_is_not_waiting_on_them_after_they_reviewed_an_older_commit()
    {
        var parts = ReviewListBuilder.Build(Login, [Pull(author: Login, reviews: [new ReviewedAt(Login, Old, NewCommits: 1)])], _ => null, _ => null);

        Assert.Equal(0, parts.WaitingCount);
    }

    [Fact]
    public void One_new_commit_is_singular() =>
        Assert.Equal("reviewed, 1 new commit", ReviewListBuilder.Classify(Login, Pull(reviews: [new ReviewedAt(Login, Old, NewCommits: 1)]))?.Text);

    [Fact]
    public void A_review_of_the_head_a_request_for_a_team_and_a_review_by_someone_else_do_not_wait()
    {
        Assert.Null(ReviewListBuilder.Classify(Login, Pull(reviews: [new ReviewedAt(Login, Head, NewCommits: 0)])));
        Assert.Null(ReviewListBuilder.Classify(Login, Pull(requested: [], reviews: [])));
        Assert.Null(ReviewListBuilder.Classify(Login, Pull(requested: ["someone-else"], reviews: [new ReviewedAt("someone-else", Old, NewCommits: 3)])));
        Assert.Null(ReviewListBuilder.Classify(Login, Pull(reviews: [new ReviewedAt(Login, Old, NewCommits: 3)], head: null)));
    }

    [Fact]
    public void The_owners_own_open_PRs_are_Yours_and_other_peoples_unrequested_PRs_are_not_shown()
    {
        var parts = Build([Pull(number: 1, author: "JOI-example"), Pull(number: 2, requested: [Login]), Pull(number: 3, author: "kim-dev")]);

        Assert.Equal(1, parts.WaitingCount);
        Assert.Equal([2], parts.Groups[0].Sections.SelectMany(static s => s.Rows).Select(static r => r.Number));
        Assert.Equal([1], parts.Groups[1].Sections.SelectMany(static s => s.Rows).Select(static r => r.Number));
        Assert.Null(parts.Empty);
    }

    [Fact]
    public void Project_sections_come_before_repo_sections_and_rows_are_newest_activity_first()
    {
        static ReviewProjectName? ProjectOf(string repo) => repo == "acme/api" ? new ReviewProjectName("prj_1", "Sindri") : null;
        var parts = Build(
            [Pull("acme/web", 10, requested: [Login], updatedDays: -1), Pull("acme/api", 20, requested: [Login], updatedDays: -3), Pull("acme/api", 21, requested: [Login], updatedDays: -1), Pull("acme/aaa", 30, requested: [Login])],
            ProjectOf);

        var sections = parts.Groups[0].Sections;
        Assert.Equal([(ReviewSectionBy.Project, "prj_1", "Sindri"), (ReviewSectionBy.Repo, null, "acme/aaa"), (ReviewSectionBy.Repo, null, "acme/web")], sections.Select(static s => (s.By, s.Key, s.Name)));
        Assert.Equal([21, 20], sections[0].Rows.Select(static r => r.Number));
    }

    [Fact]
    public void A_Yours_PR_in_a_projects_repo_lands_in_the_project_section()
    {
        var parts = Build([Pull("acme/api", 1, author: Login)], static _ => new ReviewProjectName("prj_1", "Sindri"));

        var section = Assert.Single(parts.Groups[1].Sections);
        Assert.Equal((ReviewSectionBy.Project, "Sindri"), (section.By, section.Name));
    }

    [Fact]
    public void Reviewers_never_read_give_the_notice_and_a_list_with_no_reviewers_at_all_does_not()
    {
        Assert.Equal(ReviewListBuilder.BeforeReviewersNotice, Build([Pull(author: Login, read: false)]).Notice);
        Assert.Null(Build([Pull(author: Login, read: true)]).Notice);
        Assert.Null(Build([]).Notice);
    }

    [Fact]
    public void No_rows_gives_the_empty_state_and_both_groups_with_no_sections()
    {
        var parts = Build([]);

        Assert.Equal(ReviewEmpty.Words, parts.Empty);
        Assert.Equal([ReviewGroupKind.Waiting, ReviewGroupKind.Yours], parts.Groups.Select(static g => g.Kind));
        Assert.All(parts.Groups, static g => Assert.Empty(g.Sections));
        Assert.Equal(0, parts.WaitingCount);
    }

    [Fact]
    public void A_row_carries_the_summary_and_the_claim_flag_and_its_page_path()
    {
        var summary = new ReviewSummary(Head, ReviewCi.Fail, RequiredOpen: 2, Day);
        var parts = ReviewListBuilder.Build(Login, [Pull("acme/api", 2071, requested: [Login], claims: [new ReviewClaim("doc#Ph1", Resolved: true)])], _ => summary, static _ => null);

        var row = parts.Groups[0].Sections[0].Rows[0];
        Assert.Equal((ReviewCi.Fail, 2, "2 open", true, "/reviews/acme/api/2071"), (row.Ci, row.RequiredOpen, row.RequiredText, row.ClaimsDoc, row.Href));
        Assert.Equal("none open", new ReviewSummary(Head, ReviewCi.Pass, RequiredOpen: 0, Day).RequiredText);
        Assert.Null(new ReviewSummary(Head, ReviewCi.Pass, RequiredOpen: null, Day).RequiredText);
    }

    private static ReviewRow RowFor(ReviewSummary? summary) =>
        ReviewListBuilder.Build(Login, [Pull("acme/api", number: 7, requested: [Login])], _ => summary, static _ => null).Groups[0].Sections[0].Rows[0];

    [Fact]
    public void A_row_with_no_summary_says_notRead()
    {
        var row = RowFor(summary: null);

        Assert.Equal("notRead", row.RequiredState);
        Assert.Null(row.RequiredOpen);
    }

    [Fact]
    public void A_summary_whose_settings_name_no_checks_says_noneNamed_with_no_count()
    {
        var row = RowFor(new ReviewSummary(Head, ReviewCi.Pass, RequiredOpen: null, Day));

        Assert.Equal("noneNamed", row.RequiredState);
        Assert.Null(row.RequiredOpen);
        Assert.Null(row.RequiredText);
    }

    [Fact]
    public void A_summary_with_open_checks_says_counted_and_the_count()
    {
        var row = RowFor(new ReviewSummary(Head, ReviewCi.Fail, RequiredOpen: 2, Day));

        Assert.Equal("counted", row.RequiredState);
        Assert.Equal(2, row.RequiredOpen);
    }

    [Fact]
    public void A_summary_with_none_open_says_counted_and_zero()
    {
        var row = RowFor(new ReviewSummary(Head, ReviewCi.Pass, RequiredOpen: 0, Day));

        Assert.Equal("counted", row.RequiredState);
        Assert.Equal(0, row.RequiredOpen);
    }

    [Fact]
    public void The_library_line_has_four_states()
    {
        Assert.Equal("Library: reading…", ReviewLibrary.From(new ReviewLibraryFacts(Reading: true, 0, 0, [])).Text);
        Assert.Equal("Library: no design docs yet", ReviewLibrary.From(new ReviewLibraryFacts(Reading: false, 0, 0, [])).Text);
        Assert.Equal("Library: 41 docs, 3 with errors", ReviewLibrary.From(new ReviewLibraryFacts(Reading: false, 41, 3, [])).Text);
        Assert.Equal("Library: 33 docs, 1 folder can't be read", ReviewLibrary.From(new ReviewLibraryFacts(Reading: false, 33, 0, [new ReviewUnreadableRoot("a", "x")])).Text);
        Assert.Equal("Library: 33 docs, 2 folders can't be read", ReviewLibrary.From(new ReviewLibraryFacts(Reading: false, 33, 0, [new ReviewUnreadableRoot("a", "x"), new ReviewUnreadableRoot("b", "y")])).Text);
    }
}
