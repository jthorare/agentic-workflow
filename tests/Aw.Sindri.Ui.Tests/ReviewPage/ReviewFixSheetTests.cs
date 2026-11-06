using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Reviews;

namespace Aw.Sindri.Ui.Tests.ReviewPage;

/// <summary>The fix-task sheet (review page spec §8.2, §6.3, §6.4, §16; mockups sheet-fix-*): what it pre-fills, what it says Send does, the Sending pattern, the outcomes, the half-done send and where focus goes.</summary>
[Lane(TestLane.Unit)]
public sealed class ReviewFixSheetTests
{
    private const string X = "src/Api/A/A.Core/X.cs";

    private static readonly ReviewFixTarget _mine = new("acme/api", 2071, "a1b2c3d4e5f60718293a4b5c6d7e8f9012345678", "a1b2c3d", "feat/retry-budget", "joi-example", PostsThreads: true, "sindri-pr-review");
    private static readonly ReviewFixTarget _theirs = _mine with { Author = "sam-dev", PostsThreads = false };
    private static readonly ReviewFixSource _check = new(ReviewFixKind.Check, "Recipe change shows Ui tests ran", "ci-ui", "ui-tests: failure on a1b2c3d", string.Empty, Line: null, Added: false);
    private static readonly ReviewFixSource _requirement = new(ReviewFixKind.Requirement, "Offline PRs answer from the cache", "REQ-E4", string.Empty, string.Empty, Line: null, Added: false);
    private static readonly ReviewFixSource _line = new(ReviewFixKind.Line, "var b = 3;", string.Empty, string.Empty, X, Line: 11, Added: true);
    private static readonly ReviewFocus _checkRow = new(ReviewFocusRegion.Panel, "requiredChecks:ci-ui");

    private readonly ReviewPageFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ReviewFixSheetViewModel Sheet() => new(_f.Server, _f.Clock);

    private ReviewFixSheetViewModel Opened(ReviewFixSource source, ReviewFixTarget? target = null, ReviewFocus? returnTo = null)
    {
        var sheet = Sheet();
        sheet.Open(ReviewPageFixture.Url, new ReviewFixRequestedEventArgs(source, target ?? _mine, returnTo ?? _checkRow));
        return sheet;
    }

    private static async Task WaitFor(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(10, Token);
        }
    }

    [Fact]
    public void From_a_check_the_sheet_is_prefilled_and_titles_the_item_from_the_first_line()
    {
        var sheet = Opened(_check);

        Assert.Equal(("Send as fix task", "acme/api #2071 · feat/retry-budget · a1b2c3d"), (sheet.Heading, sheet.SubLine));
        Assert.Equal("Required check failed: Recipe change shows Ui tests ran. ui-tests: failure on a1b2c3d. Fix it, or say why it doesn't apply here.", sheet.Text);
        Assert.Equal(("Fix: Required check failed: Recipe change shows …", "128 / 2,000", true), (sheet.ItemTitle, sheet.CountText, sheet.CanSend));
        Assert.Equal(new ReviewFocus(ReviewFocusRegion.FixText), sheet.Focus);
    }

    [Fact]
    public void From_a_requirement_the_sheet_says_it_is_not_covered_and_names_the_doc()
    {
        var sheet = Opened(_requirement, returnTo: new ReviewFocus(ReviewFocusRegion.Panel, "docMap:REQ-E4"));

        Assert.Equal("REQ-E4 (Offline PRs answer from the cache) in sindri-pr-review isn't covered by this PR. Cover it, or say why it waits.", sheet.Text);
        Assert.Equal(("REQ-E4  Offline PRs answer from the cache", "Requirement · sindri-pr-review · no PR claims it"), (sheet.SourceTitle, sheet.SourceDetail));
    }

    [Fact]
    public void From_a_check_the_from_box_reads_its_state_and_reason()
    {
        var sheet = Opened(_check);

        Assert.Equal(("fail  Recipe change shows Ui tests ran", "Required check · ui-tests: failure on a1b2c3d"), (sheet.SourceTitle, sheet.SourceDetail));
    }

    [Fact]
    public void From_a_diff_line_the_text_starts_empty_and_Send_waits_for_words()
    {
        var sheet = Opened(_line, returnTo: new ReviewFocus(ReviewFocusRegion.DiffLine, X + ":+11"));

        Assert.Equal((string.Empty, false, "src/Api/A/A.Core/X.cs:11", "added line"), (sheet.Text, sheet.CanSend, sheet.SourceTitle, sheet.SourceDetail));

        sheet.Text = "Release the hold when the booking is cancelled, not only when it expires.";

        Assert.True(sheet.CanSend);
        Assert.Equal("Fix: Release the hold when the booking is cancel…", sheet.ItemTitle);
    }

    [Fact]
    public void Send_says_what_it_will_post_and_where_it_goes()
    {
        var check = Opened(_check);
        var line = Opened(_line);

        Assert.Equal("Review comment on #2071 (the review body, no diff line), from Sindri, marker sindri.fix:<item id>", check.ThreadText);
        Assert.Equal("Line comment on src/Api/A/A.Core/X.cs:11, from Sindri (verdict comment, marker sindri.fix:<item id>)", line.ThreadText);
        Assert.Equal("The babysitter watching #2071, else a live session on feat/retry-budget, else the item waits in Work", check.GoesToText);
    }

    [Fact]
    public void On_a_contributors_PR_the_sheet_says_it_makes_the_item_only()
    {
        var sheet = Opened(_check, _theirs);

        Assert.Equal("Item only: Sindri won't post on sam-dev's PR", sheet.ThreadText);
    }

    [Fact]
    public void The_text_is_capped_at_2000_characters()
    {
        var sheet = Opened(_line);

        sheet.Text = new string('x', 2000);
        Assert.True(sheet.CanSend);

        sheet.Text = new string('x', 2001);
        Assert.Equal((false, "2,001 / 2,000"), (sheet.CanSend, sheet.CountText));
    }

    [Fact]
    public async Task Send_posts_the_finding_with_its_kind_head_and_source()
    {
        var check = Opened(_check);
        var requirement = Opened(_requirement);
        var line = Opened(_line);
        line.Text = "Release on cancel too.";

        await check.Send(Token);
        await requirement.Send(Token);
        await line.Send(Token);

        const string head = "a1b2c3d4e5f60718293a4b5c6d7e8f9012345678";
        Assert.Equal(
            [
                new Ui.Api.Reviews.ReviewFixSend("check", check.Text, head, "ci-ui", Path: null, Line: null, Item: null),
                new Ui.Api.Reviews.ReviewFixSend("requirement", requirement.Text, head, "REQ-E4", Path: null, Line: null, Item: null),
                new Ui.Api.Reviews.ReviewFixSend("line", "Release on cancel too.", head, SourceId: null, X, 11, Item: null),
            ],
            _f.Server.ReviewFixSends);
    }

    [Theory]
    [InlineData(ReviewFixBodies.Babysitter, "Babysitter", "Fix task sent to the babysitter on #2071. It takes the thread as work and closes the item when it's fixed.", "#2071 · review comment")]
    [InlineData(ReviewFixBodies.Session, "Session", "Fix task sent to the session on feat/retry-budget (claude, ~/repos/aw-wt/retry-budget).", "#2071 · review comment")]
    [InlineData(ReviewFixBodies.Waiting, "Waiting", "No session is on feat/retry-budget. The item waits in Work as \"Waiting on feat/retry-budget\", and the first session on that branch is told.", "#2071 · review comment")]
    public async Task A_sent_task_says_who_took_it_and_focus_goes_to_that_text(string body, string outcome, string words, string thread)
    {
        _f.Server.ReviewFixAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, body);
        var sheet = Opened(_check);

        await sheet.Send(Token);

        Assert.Equal((Enum.Parse<ReviewFixOutcome>(outcome), "Fix task sent", words, "01J9Z3T7KQ", thread), (sheet.Outcome, sheet.Heading, sheet.OutcomeText, sheet.ItemId, sheet.ThreadResult));
        Assert.Equal((false, false, new ReviewFocus(ReviewFocusRegion.FixOutcome)), (sheet.CanSend, sheet.IsSending, sheet.Focus));
    }

    [Fact]
    public async Task A_task_from_a_line_names_the_file_and_line_in_its_thread()
    {
        var sheet = Opened(_line);
        sheet.Text = "Release on cancel too.";

        await sheet.Send(Token);

        Assert.Equal("#2071 · X.cs:11", sheet.ThreadResult);
    }

    [Fact]
    public async Task On_a_contributors_PR_the_outcome_is_item_only_and_the_thread_says_not_posted()
    {
        _f.Server.ReviewFixAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, ReviewFixBodies.ItemOnly);
        var sheet = Opened(_check, _theirs);

        await sheet.Send(Token);

        Assert.Equal(
            (ReviewFixOutcome.ItemOnly, "Item only: Sindri won't post on sam-dev's PR. The item waits in Work as \"Waiting on feat/retry-budget\".", "not posted (not your PR)"),
            (sheet.Outcome, sheet.OutcomeText, sheet.ThreadResult));
    }

    [Fact]
    public async Task A_half_done_send_says_the_item_was_made_and_the_post_failed_with_Retry()
    {
        _f.Server.ReviewFixAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, ReviewFixBodies.HalfDone);
        var sheet = Opened(_check);

        await sheet.Send(Token);

        Assert.Equal(
            (ReviewFixOutcome.HalfDone, "Fix task not fully sent", "Item 01J9Z3T7KQ created in Work; posting to #2071 failed: GitHub answered 502 (bad gateway).", "SND-REVIEW-006", "not posted", true),
            (sheet.Outcome, sheet.Heading, sheet.OutcomeText, sheet.Code, sheet.ThreadResult, sheet.CanRetry));
        Assert.Equal(new ReviewFocus(ReviewFocusRegion.FixOutcome), sheet.Focus);
    }

    [Fact]
    public async Task Retry_reuses_the_item_so_the_thread_cannot_post_twice()
    {
        _f.Server.ReviewFixAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, ReviewFixBodies.HalfDone);
        var sheet = Opened(_check);
        await sheet.Send(Token);
        _f.Server.ReviewFixAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, ReviewFixBodies.Babysitter);

        await sheet.Retry(Token);

        Assert.Equal([null, "01J9Z3T7KQ"], _f.Server.ReviewFixSends.Select(s => s.Item), StringComparer.Ordinal);
        Assert.Equal((ReviewFixOutcome.Babysitter, "Fix task sent", false), (sheet.Outcome, sheet.Heading, sheet.CanRetry));
    }

    [Fact]
    public async Task A_refused_send_made_nothing_says_why_and_can_be_pressed_again()
    {
        _f.Server.ReviewFixAnswer = RouteAnswer.Refused("This caller may not run the command.");
        var sheet = Opened(_check);

        await sheet.Send(Token);

        Assert.Equal((ReviewFixOutcome.Failed, string.Empty, true, false), (sheet.Outcome, sheet.ItemId, sheet.CanSend, sheet.IsSending));
        Assert.Contains("This caller may not run the command.", sheet.OutcomeText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task After_300_ms_the_button_says_Sending_and_a_second_press_does_nothing()
    {
        _f.Server.ReviewFixGate = new TaskCompletionSource();
        var sheet = Opened(_check);

        var first = sheet.Send(Token);

        Assert.Equal((true, "Send", false), (sheet.IsSending, sheet.SendText, sheet.CanSend));
        _f.Clock.Advance(TimeSpan.FromMilliseconds(300));
        await WaitFor(() => string.Equals(sheet.SendText, "Sending…", StringComparison.Ordinal));
        Assert.Equal("Sending…", sheet.SendText);
        await sheet.Send(Token);   // the second press
        _ = Assert.Single(_f.Server.ReviewFixSends);
        _f.Server.ReviewFixGate.SetResult();
        await first;
        Assert.Equal((ReviewFixOutcome.Babysitter, false), (sheet.Outcome, sheet.IsSending));
    }

    [Fact]
    public void Esc_Cancel_and_Close_return_focus_to_the_row_the_request_came_from()
    {
        var sheet = Opened(_check);
        ReviewFocus? back = null;
        sheet.Closed += (_, e) => back = e.Focus;

        sheet.Close();

        Assert.Equal(_checkRow, back);
    }

    [Fact]
    public async Task Closing_after_a_send_returns_to_the_same_row()
    {
        var sheet = Opened(_check);
        await sheet.Send(Token);
        ReviewFocus? back = null;
        sheet.Closed += (_, e) => back = e.Focus;

        sheet.Close();

        Assert.Equal(_checkRow, back);
    }
}
