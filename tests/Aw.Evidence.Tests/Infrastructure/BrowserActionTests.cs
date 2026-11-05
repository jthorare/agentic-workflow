using Aw.Evidence.Core;
using Aw.Evidence.Core.Browser;
using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Ids;
using Aw.Evidence.Core.Scripts;
using Aw.Evidence.Core.Steps;
using Aw.Evidence.Core.Usage;
using Aw.Evidence.Infrastructure.Judging;
using Aw.Kernel.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aw.Evidence.Tests.Infrastructure;

[Lane(TestLane.Heavy)]
[Collection(SharedBrowser.Name)]
public sealed class BrowserActionTests(BrowserFixture fixture)
{
    private static readonly TimeSpan _wait = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _short = TimeSpan.FromMilliseconds(800);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_scripted_flow_fills_clicks_and_checks_the_page()
    {
        await using var session = await fixture.OpenAsync();

        Assert.True((await session.GotoAsync("/", _wait, Token)).IsSuccess);
        Assert.True((await session.FillAsync(Locator.ByTestId(TestId.From("search")), "St", _wait, Token)).IsSuccess);
        Assert.Equal("St", (await session.InputValueAsync(Locator.ByTestId(TestId.From("search")), _wait, Token)).Value);
        Assert.True((await session.WaitVisibleAsync(Locator.ByText("Hospitals"), _wait, Token)).IsSuccess, "the visible copy wins over a hidden one before it");
        Assert.True((await session.ClickAsync(Locator.ByTestId(TestId.From("open")), _wait, Token)).IsSuccess);
        Assert.True((await session.WaitVisibleAsync(Locator.ByText("Panel open"), _wait, Token)).IsSuccess);
        Assert.True((await session.ClickAsync(Locator.ByTestId(TestId.From("next")), _wait, Token)).IsSuccess);
        var moved = await session.WaitForPathAsync("/next", _wait, Token);
        Assert.True(moved.IsSuccess, moved.IsError ? moved.Error.Message : null);
        Assert.True((await session.WaitHiddenAsync(Locator.ByText("Panel open"), _wait, Token)).IsSuccess);
    }

    [Fact]
    public async Task A_broken_selector_is_broken_and_a_wrong_expectation_is_failed()
    {
        await using var session = await fixture.OpenAsync();
        _ = await session.GotoAsync("/", _wait, Token);
        var runner = new StepRunner(new UnavailableJudge(NullLogger<UnavailableJudge>.Instance), TimeProvider.System);
        var options = new StepRunOptions(_short, _short, AllowRepair: true);

        var broken = await runner.RunAsync(session, new ScriptStep(StepAction.Click, "no-such-button", new TextVisible("Panel open")), options, new InvocationLog(), Token);
        var failed = await runner.RunAsync(session, new ScriptStep(StepAction.Click, "open", new TextVisible("Never shown")), options, new InvocationLog(), Token);
        var passed = await runner.RunAsync(session, new ScriptStep(StepAction.Click, "open", new TextVisible("Panel open")), options, new InvocationLog(), Token);

        Assert.Equal(StepStatus.Broken, broken);
        Assert.Equal(StepStatus.Failed, failed);
        Assert.Equal(StepStatus.Passed, passed);
    }

    [Fact]
    public async Task Visible_candidates_are_listed_in_dom_order_and_clicked_by_index()
    {
        await using var session = await fixture.OpenAsync();
        _ = await session.GotoAsync("/", _wait, Token);

        var candidates = (await session.VisibleCandidatesAsync(Token)).Value;

        var open = Assert.Single(candidates, c => c.TestId?.Value == "open");
        Assert.Equal("button", open.Role);
        Assert.Equal("Open", open.Text);
        Assert.Equal("Search", Assert.Single(candidates, c => c.TestId?.Value == "search").AccessibleName);
        Assert.DoesNotContain(candidates, c => c.TestId?.Value == "hidden-button");
        Assert.Equal(candidates.Select(c => c.Index).Order(), candidates.Select(c => c.Index));
        Assert.True((await session.ClickAsync(new CandidateLocator(open.Index, open.TestId), _wait, Token)).IsSuccess);
        Assert.True((await session.WaitVisibleAsync(Locator.ByText("Panel open"), _wait, Token)).IsSuccess);
        var shifted = await session.ClickAsync(new CandidateLocator(open.Index, TestId.From("search")), _wait, Token);
        Assert.Contains("DOM shifted", shifted.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_candidate_without_text_is_named_by_its_value_or_title()
    {
        await using var session = await fixture.OpenAsync();
        _ = await session.GotoAsync("/controls", _wait, Token);

        var candidates = (await session.VisibleCandidatesAsync(Token)).Value;

        Assert.Equal("Delete account", Assert.Single(candidates, c => c.TestId?.Value == "submit-input").AccessibleName);
        Assert.Equal("Delete", Assert.Single(candidates, c => c.TestId?.Value == "icon").AccessibleName);
    }

    [Theory]
    [InlineData("blank-label", "Delete")]
    [InlineData("blank-title", "Archive")]
    public async Task A_blank_aria_label_or_title_falls_through_to_the_value_so_a_preview_run_refuses_the_candidate(string testId, string label)
    {
        await using var session = await fixture.OpenAsync();
        _ = await session.GotoAsync("/controls", _wait, Token);

        var candidates = (await session.VisibleCandidatesAsync(Token)).Value;

        var blank = Assert.Single(candidates, c => c.TestId?.Value == testId);
        Assert.Equal(label, blank.AccessibleName);
        Assert.True(StepGuard.LabelLooksLikeWrite(blank.AccessibleName), "the repair write check refuses it under a host override");
    }

    [Fact]
    public async Task A_text_or_password_field_is_never_named_by_what_was_typed_into_it()
    {
        await using var session = await fixture.OpenAsync();
        _ = await session.GotoAsync("/controls", _wait, Token);

        var candidates = (await session.VisibleCandidatesAsync(Token)).Value;

        Assert.True(string.IsNullOrEmpty(Assert.Single(candidates, c => c.TestId?.Value == "typed").AccessibleName));
        Assert.True(string.IsNullOrEmpty(Assert.Single(candidates, c => c.TestId?.Value == "secret").AccessibleName));
    }

    [Fact]
    public async Task A_candidates_name_and_text_are_capped_at_200_code_points_so_a_page_wide_container_fits_the_judge()
    {
        await using var session = await fixture.OpenAsync();
        _ = await session.GotoAsync("/controls", _wait, Token);

        var candidates = (await session.VisibleCandidatesAsync(Token)).Value;

        var capped = string.Concat(Enumerable.Repeat("\U0001F600", 200)) + "\u2026";
        var button = Assert.Single(candidates, c => c.TestId?.Value == "long");
        Assert.Equal((capped, capped), (button.AccessibleName, button.Text));
    }

    [Fact]
    public async Task Navigation_off_the_host_is_aborted()
    {
        await using var session = await fixture.OpenAsync();
        _ = await session.GotoAsync("/", _wait, Token);

        _ = await session.ClickAsync(Locator.ByTestId(TestId.From("away")), _wait, Token);
        await Task.Delay(500, Token);

        Assert.DoesNotContain(fixture.Elsewhere.Requests, r => string.Equals(r, "/away", StringComparison.Ordinal));
        Assert.True((await session.WaitForPathAsync("/", _short, Token)).IsSuccess, "the page stays on the host");
    }

    [Fact]
    public async Task A_goto_target_that_could_leave_the_host_is_refused()
    {
        await using var session = await fixture.OpenAsync();

        var refused = await session.GotoAsync("//evil.test/", _wait, Token);

        Assert.Equal(EvidenceErrors.BrowserFailed.Code, refused.Error.Code);
        Assert.Contains("path on the host", refused.Error.Message, StringComparison.Ordinal);
    }
}
