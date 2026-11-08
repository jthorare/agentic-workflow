using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Tests.Linear;

/// <summary>The Linear issue sheet's read (Linear hub spec §3, "Issue sheet"): the field grid, the description and the comments, and what it says when the issue is not synced, removed or the API is down.</summary>
[Lane(TestLane.Unit)]
public sealed class LinearIssueSheetTests
{
    private readonly LinearIssueSheetFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_sheet_shows_the_key_the_title_and_the_field_grid_of_the_full_issue()
    {
        var sheet = await _f.Opened("ENG-12", Token);

        Assert.Equal((FramePhase.Shown, "ENG-12", "Retry budget for the sync worker"), (sheet.Phase, sheet.Key, sheet.Title));
        Assert.Equal(
            ["State: In Progress", "Priority: Urgent", "Assignee: Joi Example", "Cycle: Cycle 14", "Labels: bug, api", "Project: Sync worker"],
            sheet.Fields.Select(f => f.Name),
            StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_field_the_issue_has_no_value_for_reads_as_a_dash()
    {
        var issue = LinearIssueSheetFixture.Golden("full");
        issue["assignee"] = null;
        issue["project"] = null;
        issue["cycle"] = null;
        issue["labels"] = new System.Text.Json.Nodes.JsonArray();
        issue["priority"] = 0;
        _f.Server.LinearIssueAnswer = LinearIssueSheetFixture.Served(issue);

        var sheet = await _f.Opened("ENG-12", Token);

        Assert.Equal(
            ["State: In Progress", "Priority: No priority", "Assignee: Unassigned", "Cycle: –", "Labels: –", "Project: –"],
            sheet.Fields.Select(f => f.Name),
            StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_comments_come_newest_first_with_author_age_and_who_wrote_them()
    {
        var sheet = await _f.Opened("ENG-12", Token);

        Assert.Equal(
            [("Sam Lee", "30m ago", false), ("Joi Example", "3h ago", true)],
            sheet.Comments.Select(c => (c.Author, c.When, c.FromSindri)));
        Assert.StartsWith("@Joi Example the budget", sheet.Comments[0].Lines[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_sheet_names_the_key_and_says_loading_before_any_answer()
    {
        _f.Sheet.Show("ENG-12");

        Assert.Equal((FramePhase.Loading, "ENG-12", "Loading…"), (_f.Sheet.Phase, _f.Sheet.Key, _f.Sheet.LoadingText));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task A_PR_chip_carries_its_review_state_as_words_and_asks_for_the_review_page()
    {
        var sheet = await _f.Opened("ENG-12", Token);
        var asked = new List<(string, int)>();
        sheet.PrRequested += (_, e) => asked.Add((e.Repo, e.Number));

        sheet.OpenPr();

        Assert.Equal(("PR #2071 waiting on you", true), (sheet.PrText, sheet.CanOpenPr));
        Assert.Equal([("acme/api", 2071)], asked);
    }

    [Fact]
    public async Task A_PR_that_is_not_on_GitHub_is_plain_text()
    {
        var issue = LinearIssueSheetFixture.Golden("full");
        issue["pr"]!["url"] = "https://git.example.com/acme/api/pull/2071";
        _f.Server.LinearIssueAnswer = LinearIssueSheetFixture.Served(issue);

        var sheet = await _f.Opened("ENG-12", Token);

        Assert.Equal((true, false), (sheet.HasPr, sheet.CanOpenPr));
    }

    [Theory]
    [InlineData("https://linear.app/acme/issue/ENG-99")]
    [InlineData("https://eu.linear.app/acme/issue/ENG-99")]
    public async Task An_issue_that_is_not_synced_says_so_and_offers_Open_in_Linear_at_the_address_the_404_names(string url)
    {
        _f.Server.LinearIssueAnswer = LinearIssueSheetFixture.NotFound(url);

        var sheet = await _f.Opened("ENG-99", Token);
        await sheet.OpenInLinear(Token);

        Assert.Equal((FramePhase.NotFound, "ENG-99 isn't synced yet", true, "Open in Linear"), (sheet.Phase, sheet.ProblemText, sheet.CanOpenInLinear, sheet.OpenInLinearText));
        Assert.Equal([url], _f.Launcher.Opened);
    }

    // No address, or one that isn't https on linear.app: no button, and no guessed address is ever opened.
    [Theory]
    [InlineData(null)]
    [InlineData("https://evil.example/acme/issue/ENG-99")]
    [InlineData("http://linear.app/acme/issue/ENG-99")]
    [InlineData("https://me@linear.app/acme/issue/ENG-99")]
    [InlineData("https://lin\u0435ar.app/acme/issue/ENG-99")]
    [InlineData("https://linear.app.evil.example/acme/issue/ENG-99")]
    [InlineData("https://evillinear.app/acme/issue/ENG-99")]
    [InlineData("linear.app/acme/issue/ENG-99")]
    public async Task A_404_without_a_Linear_address_offers_no_Open_in_Linear(string? url)
    {
        _f.Server.LinearIssueAnswer = url is null ? LinearIssueSheetFixture.NotFound() : LinearIssueSheetFixture.NotFound(url);

        var sheet = await _f.Opened("ENG-99", Token);
        await sheet.OpenInLinear(Token);

        Assert.Equal((FramePhase.NotFound, "ENG-99 isn't synced yet", false), (sheet.Phase, sheet.ProblemText, sheet.CanOpenInLinear));
        Assert.Empty(_f.Launcher.Opened);
    }

    [Fact]
    public async Task A_removed_issue_shows_the_last_copy_with_a_note_that_Linear_no_longer_serves_it()
    {
        _f.Server.LinearIssueAnswer = RouteAnswer.Api("linear-issue", "removed");

        var sheet = await _f.Opened("ENG-9", Token);

        Assert.Equal((FramePhase.Shown, "Old onboarding checklist"), (sheet.Phase, sheet.Title));
        Assert.Equal("ENG-9 is no longer served by Linear. This is the last copy Sindri stored.", sheet.RemovedNote);
    }

    [Fact]
    public async Task Any_other_failure_says_Sindri_isnt_answering()
    {
        _f.Server.LinearIssueAnswer = new RouteAnswer(HttpStatusCode.InternalServerError, "{}");

        var sheet = await _f.Opened("ENG-12", Token);

        Assert.Equal((FramePhase.Refused, "Sindri isn't answering", false), (sheet.Phase, sheet.ProblemText, sheet.CanOpenInLinear));
    }

    [Fact]
    public async Task A_failing_refresh_keeps_the_issue_and_adds_a_note()
    {
        var sheet = await _f.Opened("ENG-12", Token);
        _f.Server.LinearIssueAnswer = new RouteAnswer(HttpStatusCode.InternalServerError, "{}");

        await sheet.Refresh(LinearIssueSheetFixture.Url, Token);

        Assert.Equal((FramePhase.Shown, "Retry budget for the sync worker", "Sindri isn't answering"), (sheet.Phase, sheet.Title, sheet.Note));
    }

    [Fact]
    public async Task A_refresh_draws_the_newer_read()
    {
        var sheet = await _f.Opened("ENG-12", Token);
        var newer = LinearIssueSheetFixture.Golden("full");
        newer["title"] = "Retry budget, per hour";
        _f.Server.LinearIssueAnswer = LinearIssueSheetFixture.Served(newer);

        await sheet.Refresh(LinearIssueSheetFixture.Url, Token);

        Assert.Equal("Retry budget, per hour", sheet.Title);
    }
}
