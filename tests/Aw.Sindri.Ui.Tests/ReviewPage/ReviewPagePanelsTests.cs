using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Reviews;

namespace Aw.Sindri.Ui.Tests.ReviewPage;

/// <summary>The panels (review page spec §6.2, §6.3, §6.4): order and titles, every panel's states, the rows each draws, and the collapsed set.</summary>
[Lane(TestLane.Unit)]
public sealed class ReviewPagePanelsTests
{
    private readonly ReviewPageFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ReviewPanelViewModel Panel(ReviewPageViewModel page, string id) => page.Panels.Single(p => string.Equals(p.Id, id, StringComparison.Ordinal));

    [Fact]
    public async Task The_panels_follow_the_served_order_each_with_its_title_and_the_strips_summary()
    {
        var page = await _f.Opened(Token);

        Assert.Equal(
            [("requiredChecks", "Required checks", "4 open"), ("docMap", "Design doc", "Covers Ph5, REQ-E3"), ("testPlan", "Test plan", ""), ("ci", "CI", "1 failing"), ("evidence", "Evidence", ""), ("risk", "Risk", "1 risky file"), ("conversation", "Conversation", "")],
            page.Panels.Select(p => (p.Id, p.Title, p.Summary)));
        Assert.All(page.Panels, p => Assert.Equal(ReviewPanelState.Ready, p.State));
    }

    [Fact]
    public async Task A_different_settings_order_is_the_order_drawn()
    {
        var page = await _f.Opened("full", json =>
        {
            var panels = json["panels"]!.AsArray();
            var risk = panels[5]!.DeepClone();
            panels.RemoveAt(5);
            panels.Insert(0, risk);
        }, Token);

        Assert.Equal("risk", page.Panels[0].Id);
        Assert.Equal("requiredChecks", page.Panels[1].Id);
    }

    [Fact]
    public async Task A_required_check_shows_its_state_as_a_word_and_a_glyph_and_reads_its_reason()
    {
        var page = await _f.Opened(Token);

        var checks = Panel(page, "requiredChecks").Checks;
        Assert.Equal(
            [("Recipe change shows Ui tests ran", "fail", "ui-tests: failure on a1b2c3d"), ("I ran it myself", "needs a tick", ""), ("Build passes", "running", "build: no run on a1b2c3d yet"), ("Proof is in the PR body", "claimed in body", "claimed in body"), ("Lint passes", "pass", "lint: success on a1b2c3d")],
            checks.Select(c => (c.Title, c.StatusWord, c.Detail)));
        Assert.Equal((StateGlyph.Down, StateGlyph.Ok), (checks[0].Glyph, checks[4].Glyph));
        Assert.All(checks, c => Assert.NotEqual(StateGlyph.None, c.Glyph));
        Assert.Equal("Recipe change shows Ui tests ran: fail, ui-tests: failure on a1b2c3d", checks[0].AutomationName);
        Assert.Equal("I ran it myself: needs a tick", checks[1].AutomationName);
    }

    [Fact]
    public async Task Send_as_fix_task_is_on_the_failed_check_only()
    {
        var page = await _f.Opened(Token);

        Assert.Equal([true, false, false, false, false], Panel(page, "requiredChecks").Checks.Select(c => c.CanSendFix));
    }

    [Fact]
    public async Task The_doc_map_marks_this_PRs_items_other_PRs_claims_and_unclaimed_requirements()
    {
        var page = await _f.Opened(Token);

        var doc = Panel(page, "docMap");
        Assert.Equal("Covers Ph5, REQ-E3", doc.ClaimsText);
        Assert.Equal([("Ph4", "claimed by #2060, merged", false), ("Ph5", "this PR", true)], doc.DocItems.Select(i => (i.Id, i.Mark, i.IsClaimedHere)));
        var ph5 = doc.DocItems[1];
        Assert.Equal([("REQ-E3", "this PR", false), ("REQ-E4", "unclaimed", true)], ph5.Children.Select(i => (i.Id, i.Mark, i.CanSendFix)));
    }

    [Fact]
    public async Task The_test_plan_lists_its_rows_with_what_they_verify_and_their_ticks()
    {
        var page = await _f.Opened(Token);

        var rows = Panel(page, "testPlan").Lines;
        Assert.Equal(["GET /reviews/acme/api/2071 answers every panel", "An offline PR answers from the cache"], rows.Select(r => r.Primary), StringComparer.Ordinal);
        Assert.Equal(["TP3, verifies REQ-E3", "TP4, verifies REQ-E4"], rows.Select(r => r.Secondary), StringComparer.Ordinal);   // GUESS: the second words are the row id and what it verifies
        Assert.Equal([StateGlyph.None, StateGlyph.None], rows.Select(r => r.Glyph));
    }

    [Fact]
    public async Task The_CI_panel_lists_each_run_with_its_conclusion_and_a_link_to_the_run()
    {
        var page = await _f.Opened(Token);

        var runs = Panel(page, "ci").Lines;
        Assert.Equal([("ui-tests", "failure on a1b2c3d", StateGlyph.Down), ("lint", "success on a1b2c3d", StateGlyph.Ok)], runs.Select(r => (r.Primary, r.Secondary, r.Glyph)));
        Assert.Equal("https://github.com/acme/api/pull/2071/checks", runs[0].Url);
    }

    [Theory]
    [InlineData("skipped", "neutral", "Ok", "Ok", "Ok")]
    [InlineData("cancelled", "neutral", "Down", "Ok", "Down")]
    [InlineData("skipped", "timed_out", "Ok", "Down", "Down")]
    public async Task Skipped_and_neutral_runs_are_fine_and_cancelled_and_timed_out_runs_are_down_in_the_panel_and_the_strip(string first, string second, string firstGlyph, string secondGlyph, string strip)
    {
        var page = await _f.Opened("full", json =>
        {
            var runs = json["panels"]!.AsArray().Single(p => (string?)p!["id"] == "ci")!["data"]!["runs"]!.AsArray();
            runs[0]!["conclusion"] = first;
            runs[1]!["conclusion"] = second;
        }, Token);

        Assert.Equal([firstGlyph, secondGlyph], Panel(page, "ci").Lines.Select(l => l.Glyph.ToString()), StringComparer.Ordinal);
        Assert.Equal(strip, page.Strip.Single(s => string.Equals(s.Panel, "ci", StringComparison.Ordinal)).Glyph.ToString());
    }

    [Fact]
    public async Task A_repeated_fix_button_is_named_by_its_row_and_a_files_buttons_by_its_path()
    {
        var page = await _f.Opened(Token);

        var check = Panel(page, "requiredChecks").Checks.First(c => c.CanSendFix);
        Assert.Equal("Send as fix task: " + check.Title, check.FixName);
        var deleted = page.Diff.Groups.SelectMany(g => g.Files).Single(f => f.HasShow);
        Assert.Equal(("Show " + deleted.Path, "Open on GitHub: " + deleted.Path), (deleted.ShowName, deleted.OpenName));
    }

    [Fact]
    public async Task The_evidence_risk_and_conversation_panels_list_their_rows()
    {
        var page = await _f.Opened(Token);

        Assert.Equal(["Reviews list, 1,440 px", "Proof in the PR body"], Panel(page, "evidence").Lines.Select(l => l.Primary), StringComparer.Ordinal);
        Assert.Equal("https://github.com/acme/api/pull/2071#issuecomment-1", Panel(page, "evidence").Lines[0].Url);
        Assert.Equal([("src/Api/Migrations/0042_retry.sql", "Migrations change stored data")], Panel(page, "risk").Lines.Select(l => (l.Primary, l.Secondary)));
        var thread = Assert.Single(Panel(page, "conversation").Lines);
        Assert.Equal("src/Api/A/A.Core/X.cs:12", thread.Primary);
        Assert.StartsWith("kim-dev", thread.Secondary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Every_panels_empty_state_shows_its_served_words()
    {
        _f.Serve("empty-panels");

        var page = await _f.Opened(Token);

        Assert.Equal(
            [
                "No required checks for this project", "This PR names no design doc", "No test plan rows for the claimed items", "No CI runs on a1b2c3d yet",
                "No evidence found for a1b2c3d", "No risky paths touched", "No threads yet",
            ],
            page.Panels.Select(p => p.Text),
            StringComparer.Ordinal);
        Assert.All(page.Panels, p => Assert.Equal(ReviewPanelState.Empty, p.State));
    }

    [Fact]
    public async Task A_failing_panel_shows_its_error_words_and_the_others_still_show_their_rows()
    {
        _f.Serve("panel-errors");

        var page = await _f.Opened(Token);

        Assert.Equal(
            [("requiredChecks", ReviewPanelState.Error, "Couldn't decide checks: github failed: github returned 502"), ("ci", ReviewPanelState.Error, "Couldn't read CI: github sent no check runs for a1b2c3d"), ("evidence", ReviewPanelState.Ready, ""), ("risk", ReviewPanelState.Empty, "No risky paths touched")],
            page.Panels.Where(p => new[] { "requiredChecks", "ci", "evidence", "risk" }.Contains(p.Id, StringComparer.Ordinal)).Select(p => (p.Id, p.State, p.Text)));
        Assert.Equal(ReviewPanelState.Error, Panel(page, "conversation").State);
        Assert.NotEmpty(Panel(page, "evidence").Lines);
    }

    [Fact]
    public async Task Wide_opens_every_panel_and_narrow_opens_only_required_checks()
    {
        var wide = await _f.Opened(Token, wide: true);
        var narrow = await _f.Opened(Token, wide: false);

        Assert.DoesNotContain(wide.Panels, p => p.IsCollapsed);
        Assert.Equal(["requiredChecks"], narrow.Panels.Where(p => !p.IsCollapsed).Select(p => p.Id), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_collapsed_panel_stays_collapsed_on_the_next_PR_of_the_same_settings_and_not_on_other_settings()
    {
        var first = await _f.Opened(Token, wide: true);
        Panel(first, "risk").IsCollapsed = true;

        var same = await _f.Opened(Token, wide: true);
        var other = await _f.Opened("full", json => json["settings"]!["name"] = "harbor", Token, wide: true);

        Assert.True(Panel(same, "risk").IsCollapsed);
        Assert.False(Panel(other, "risk").IsCollapsed);
    }
}
