using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Reviews;

namespace Aw.Sindri.Ui.Tests.ReviewPage;

/// <summary>The page's own states (review page spec §6.3, §16): loading, the whole-page refusals, the cached page, a moving head, and the owner's own PR.</summary>
[Lane(TestLane.Unit)]
public sealed class ReviewPageStateTests
{
    private readonly ReviewPageFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_page_is_loading_with_four_strip_placeholders_and_seven_panel_placeholders_until_its_answer()
    {
        _f.Server.ReviewPageGate = new TaskCompletionSource();
        var page = _f.Page();

        var open = page.Open(ReviewPageFixture.Url, ReviewPageFixture.Repo, ReviewPageFixture.Number, Token);

        Assert.Equal(ReviewPageState.Loading, page.State);
        Assert.Equal(["Reading checks…", "Reading the claim…", "Reading risk…", "Reading CI…"], page.Strip.Select(s => s.Text), StringComparer.Ordinal);
        Assert.All(page.Strip, s => Assert.True(s.IsLoading));
        Assert.Equal(["requiredChecks", "docMap", "testPlan", "ci", "evidence", "risk", "conversation"], page.Panels.Select(p => p.Id), StringComparer.Ordinal);
        Assert.All(page.Panels, p => Assert.Equal((ReviewPanelState.Loading, "Reading…"), (p.State, p.Text)));
        Assert.Equal((ReviewPanelState.Loading, "Reading files…"), (page.Diff.State, page.Diff.Text));
        _f.Server.ReviewPageGate.SetResult();
        await open;
        Assert.Equal(ReviewPageState.Ready, page.State);
    }

    [Fact]
    public async Task A_PR_the_forge_does_not_have_is_the_not_found_page()
    {
        _f.Refuse("not-found");

        var page = await _f.Opened(Token);

        Assert.Equal((ReviewPageState.NotFound, Labels.ReviewNotFoundHeading, "PR not found"), (page.State, page.StateHeading, page.StateText));
    }

    [Fact]
    public async Task A_repo_Sindri_does_not_sync_is_the_not_served_page_with_the_refusals_words()
    {
        _f.Refuse("not-synced");

        var page = await _f.Opened(Token);

        Assert.Equal((ReviewPageState.NotSynced, Labels.ReviewNotSyncedHeading, "Sindri doesn't sync acme/other. Add it to `context.sources.github.repos`."), (page.State, page.StateHeading, page.StateText));
    }

    [Fact]
    public async Task Before_review_login_is_set_the_whole_page_is_the_login_prompt()
    {
        _f.Refuse("no-login");

        var page = await _f.Opened(Token);

        Assert.Equal((ReviewPageState.LoginPrompt, "Set your GitHub login", "Set your GitHub login on the Reviews tab first."), (page.State, page.StateHeading, page.StateText));
        Assert.Empty(page.Strip);
    }

    [Fact]
    public async Task Offline_with_nothing_cached_says_so_and_offers_nothing_to_read()
    {
        // GUESS: the API's refusal for an offline, uncached PR is a 503 SND-REVIEW-503; the spec names only the words (§16)
        _f.Server.ReviewPageAnswer = RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-REVIEW-503", "Offline, and this PR isn't cached yet");

        var page = await _f.Opened(Token);

        Assert.Equal((ReviewPageState.OfflineUncached, "Offline, and this PR isn't cached yet"), (page.State, page.StateHeading));
        Assert.Empty(page.Panels);
    }

    [Fact]
    public async Task A_Sindri_that_is_down_is_a_failed_page_with_words()
    {
        _f.Server.GoDown();

        var page = await _f.Opened(Token);

        Assert.Equal(ReviewPageState.Failed, page.State);
        Assert.NotEmpty(page.StateText);
    }

    [Fact]
    public async Task A_cached_page_shows_the_page_with_its_as_of_note()
    {
        _f.Serve("offline-cached");

        var page = await _f.Opened(Token);

        Assert.Equal((ReviewPageState.Ready, "as of 23:45, the PR may have moved", null), (page.State, page.OfflineText, page.MovingText));
        Assert.NotEmpty(page.Diff.Groups);
    }

    [Fact]
    public async Task A_head_that_moved_twice_shows_the_moving_line_over_the_newest_reads()
    {
        _f.Serve("moving");

        var page = await _f.Opened(Token);

        Assert.Equal((ReviewPageState.Ready, "The PR is moving; reload when it settles.", null), (page.State, page.MovingText, page.OfflineText));
    }

    [Fact]
    public async Task Reload_reads_again_and_focus_lands_on_the_strips_first_part()
    {
        var page = await _f.Opened(Token);

        await page.Reload(Token);

        Assert.Equal(2, _f.Server.ReviewPageReads.Count);
        Assert.Equal(new ReviewFocus(ReviewFocusRegion.StripFirst), page.Focus);
    }

    [Fact]
    public async Task On_someone_elses_PR_both_review_buttons_are_on_and_open_the_PRs_review_box_on_GitHub()
    {
        var page = await _f.Opened(Token);

        Assert.Equal((true, true, string.Empty), (page.ApproveEnabled, page.RequestChangesEnabled, page.OwnPrText));
        Assert.StartsWith("https://github.com/acme/api/pull/2071", page.ReviewUrl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task On_the_owners_own_PR_approve_and_request_changes_are_off_and_say_why()
    {
        _f.Serve("own-pr");

        var page = await _f.Opened(Token);

        Assert.True(page.IsYours);
        Assert.Equal((false, false, "You can't approve or request changes on your own PR: GitHub takes a comment only"), (page.ApproveEnabled, page.RequestChangesEnabled, page.OwnPrText));
    }

    [Fact]
    public async Task On_an_agent_PR_the_fix_thread_is_posted_while_the_review_buttons_follow_only_yours()
    {
        // an agent's PR: its author is the forge credential's login, not review.login, so yours is false and postsFixThreads is true
        var page = await _f.Opened("full", json => json["pr"]!["postsFixThreads"] = true, Token);

        Assert.Equal((false, true), (page.IsYours, page.FixTarget!.PostsThreads));
        Assert.Equal((true, true, string.Empty), (page.ApproveEnabled, page.RequestChangesEnabled, page.OwnPrText));   // spec §16 pick 4: disabled only on the owner's own PR
    }
}
