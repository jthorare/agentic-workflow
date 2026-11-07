using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Linear;

namespace Aw.Sindri.Ui.Tests.Linear;

/// <summary>What the issue sheet shows for each answer to a save (Linear hub spec §3 states table, §5.4, §8): applied, conflict, unknown, rate limit and the refusals.</summary>
[Lane(TestLane.Unit)]
public sealed class LinearIssueEditOutcomeTests
{
    private readonly LinearIssueEditFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task While_saving_the_button_says_sending_and_every_field_locks()
    {
        var sheet = await _f.Opened(Token);
        _f.Server.LinearChangeGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _f.Pick("Priority", "High");

        var saving = sheet.Save(Token);

        Assert.Equal((true, "Sending…", false, true), (sheet.IsSaving, sheet.SaveText, sheet.CanSave, sheet.IsLocked));
        Assert.False(_f.Field("State").IsEnabled || sheet.TitleField.IsEnabled);
        _f.Server.LinearChangeGate.SetResult();
        await saving;
        Assert.Equal((false, "Save"), (sheet.IsSaving, sheet.SaveText));
    }

    [Fact]
    public async Task Applied_replaces_the_fields_from_the_answer_says_saved_with_its_time_and_focuses_the_title()
    {
        var sheet = await _f.Opened(Token);
        _f.Pick("State", "In Review");
        _f.Pick("Priority", "Urgent");

        await sheet.Save(Token);

        Assert.Equal(("In Review", false, string.Empty), (_f.Field("State").Selected?.Name, sheet.HasChanges, sheet.DirtyLine));
        Assert.Equal($"Saved. The fields show Linear's answer ({LinearIssueEditFixture.Local("2026-01-01T10:05:00Z")}).", sheet.StatusLine);
        Assert.Equal([LinearIssueFocus.Title], _f.Focus);
    }

    [Fact]
    public async Task A_store_warning_says_it_was_saved_in_linear_and_will_show_after_the_next_sync()
    {
        var sheet = await _f.Opened(Token);
        _f.Server.QueueLinearChange(LinearIssueEditFixture.Change("applied-store-failed"));
        _f.Pick("State", "In Review");

        await sheet.Save(Token);

        Assert.Equal("Saved in Linear; Sindri shows it after the next sync.", sheet.StatusLine);
    }

    [Fact]
    public async Task An_already_applied_answer_says_linear_has_the_change()
    {
        var sheet = await _f.Opened(Token);
        _f.Server.QueueLinearChange(LinearIssueEditFixture.Change("already-applied"));
        _f.Pick("State", "In Review");

        await sheet.Save(Token);

        Assert.Equal("Already saved. Linear has your change.", sheet.StatusLine);
    }

    [Fact]
    public async Task A_conflict_names_the_issue_and_time_sends_nothing_more_and_moves_focus_to_the_banner()
    {
        var sheet = await _f.Opened(Token);
        _f.Server.QueueLinearChange(LinearIssueEditFixture.PriorityConflict(3));
        _f.Pick("Priority", "High");

        await sheet.Save(Token);

        Assert.Equal((true, $"ENG-12 changed in Linear at {LinearIssueEditFixture.Local("2026-01-01T10:01:00Z")}.", "Nothing was sent. The fields below differ from what you opened; Linear's value is next to yours."),
            (sheet.HasConflict, sheet.ConflictHeadline, sheet.ConflictNote));
        Assert.Equal([LinearIssueFocus.Banner], _f.Focus);
        Assert.Equal((false, true), (sheet.IsLocked, sheet.HasChanges));
    }

    [Fact]
    public async Task A_conflicting_field_shows_linears_value_beside_yours_and_untouched_fields_take_the_live_issue()
    {
        var sheet = await _f.Opened(Token);
        _f.Server.QueueLinearChange(LinearIssueEditFixture.PriorityConflict(3));
        _f.Pick("Priority", "High");

        await sheet.Save(Token);

        var row = Assert.Single(sheet.ConflictRows);
        Assert.Equal(("Priority", "Linear: Medium · yours: High"), (row.Label, row.Text));
        Assert.Equal(("Done", "High"), (_f.Field("State").Selected?.Name, _f.Field("Priority").Selected?.Name));
        Assert.False(_f.Field("State").IsChanged);
    }

    [Fact]
    public async Task Use_linears_takes_linears_value_for_that_field_and_drops_its_row()
    {
        var sheet = await _f.Opened(Token);
        _f.Server.QueueLinearChange(LinearIssueEditFixture.PriorityConflict(3));
        _f.Pick("Priority", "High");
        await sheet.Save(Token);

        sheet.ConflictRows[0].UseLinears();

        Assert.Equal(("Medium", false, false), (_f.Field("Priority").Selected?.Name, sheet.HasConflict, sheet.HasChanges));
    }

    [Fact]
    public async Task Use_linears_for_all_takes_every_listed_field_and_never_sends_a_state_id()
    {
        var sheet = await _f.Opened(Token);
        _f.Server.QueueLinearChange(LinearIssueEditFixture.PriorityConflict(3));
        _f.Pick("Priority", "High");
        _f.Pick("Assignee", "Sam Lee");
        await sheet.Save(Token);

        sheet.UseLinearsForAll();
        await sheet.Save(Token);

        Assert.Equal(("Medium", false), (_f.Field("Priority").Selected?.Name, sheet.HasConflict));
        Assert.Equal(2, _f.Server.LinearChanges.Count);
        Assert.Equal("""{"assigneeId":"00000000-0000-4000-8000-0000000000a2"}""", _f.Server.LinearChanges[1].Body.Set.ToJsonString());
        Assert.False(_f.Server.LinearChanges[1].Body.Set.ContainsKey("stateId") || _f.Server.LinearChanges[1].Body.Opened.ContainsKey("stateId"));
    }

    [Fact]
    public async Task Save_mine_anyway_sends_my_values_again_against_the_values_linear_just_showed()
    {
        var sheet = await _f.Opened(Token);
        _f.Server.QueueLinearChange(LinearIssueEditFixture.PriorityConflict(3));
        _f.Pick("Priority", "High");
        await sheet.Save(Token);

        await sheet.SaveMineAnyway(Token);

        Assert.Equal(2, _f.Server.LinearChanges.Count);
        Assert.Equal(("""{"priority":3}""", """{"priority":2}"""), (_f.Server.LinearChanges[1].Body.Opened.ToJsonString(), _f.Server.LinearChanges[1].Body.Set.ToJsonString()));
        Assert.False(sheet.HasConflict);
    }

    [Fact]
    public async Task An_unknown_answer_keeps_the_form_locked_until_retry_which_sends_the_identical_body()
    {
        var sheet = await _f.Opened(Token);
        _f.Server.QueueLinearChange(LinearIssueEditFixture.Change("unknown"));
        _f.Pick("Priority", "High");

        await sheet.Save(Token);

        Assert.Equal(("Linear didn't confirm it. Retry?", true, true), (sheet.StatusLine, sheet.CanRetry, sheet.IsLocked));
        await sheet.Retry(Token);
        Assert.Equal(2, _f.Server.LinearChanges.Count);
        Assert.Equal((_f.Server.LinearChanges[0].Body.Opened.ToJsonString(), _f.Server.LinearChanges[0].Body.Set.ToJsonString()), (_f.Server.LinearChanges[1].Body.Opened.ToJsonString(), _f.Server.LinearChanges[1].Body.Set.ToJsonString()));
        Assert.Equal((false, false), (sheet.CanRetry, sheet.IsLocked));
    }

    [Fact]
    public Task After_fifteen_seconds_without_an_answer_the_sheet_says_it_is_checking() => UiThread.Run(
        async () =>
        {
            var f = new LinearIssueEditFixture();
            var sheet = await f.Opened(Token);
            f.Server.LinearChangeGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            f.Pick("Priority", "High");

            var saving = sheet.Save(Token);
            f.Clock.Advance(TimeSpan.FromSeconds(14));
            Assert.Equal(string.Empty, sheet.StatusLine);
            f.Clock.Advance(TimeSpan.FromSeconds(1));
            Assert.Equal("Linear didn't confirm it. Checking ENG-12…", sheet.StatusLine);

            f.Server.LinearChangeGate.SetResult();
            await saving;
            Assert.StartsWith("Saved.", sheet.StatusLine, StringComparison.Ordinal);
        },
        Token);

    [Fact]
    public async Task A_rate_limit_shows_the_time_to_try_again_and_keeps_the_form_open()
    {
        var sheet = await _f.Opened(Token);
        _f.Server.QueueLinearChange(LinearIssueEditFixture.Change("rate-limited"));
        _f.Pick("Priority", "High");

        await sheet.Save(Token);

        Assert.Equal($"Linear's rate limit: try again after {LinearIssueEditFixture.Local("2026-01-01T10:06:00Z")}", sheet.ErrorBanner);
        Assert.Equal((false, true, "Save"), (sheet.IsLocked, sheet.HasChanges, sheet.SaveText));
        Assert.True(sheet.CanSave);
    }

    [Fact]
    public async Task A_value_linear_refuses_shows_the_error_text_and_keeps_the_form_with_my_edits()
    {
        var sheet = await _f.Opened(Token);
        _f.Server.QueueLinearChange(LinearIssueEditFixture.Change("bad-value"));
        _f.Pick("Priority", "High");

        await sheet.Save(Token);

        Assert.Contains("stateId isn't one of ENG's states", sheet.ErrorBanner, StringComparison.Ordinal);
        Assert.Equal(("High", false, true), (_f.Field("Priority").Selected?.Name, sheet.IsLocked, sheet.CanSave));
    }

    [Fact]
    public async Task A_down_linear_and_a_refused_key_say_so_in_the_banner()
    {
        var sheet = await _f.Opened(Token);
        _f.Server.QueueLinearChange(new RouteAnswer(HttpStatusCode.ServiceUnavailable, """{"title":"SND-LINEAR-010","status":503,"detail":"x"}"""), new RouteAnswer(HttpStatusCode.BadGateway, """{"title":"SND-LINEAR-004","status":502,"detail":"x"}"""));
        _f.Pick("Priority", "High");

        await sheet.Save(Token);
        Assert.Equal("Couldn't reach Linear, so nothing was changed. Try again.", sheet.ErrorBanner);
        await sheet.Save(Token);
        Assert.Equal("Linear refused the write key. Make a new one with write permission.", sheet.ErrorBanner);
    }

    [Fact]
    public async Task A_conflict_whose_live_issue_has_no_write_member_turns_the_form_read_only_after_showing_it()
    {
        var sheet = await _f.Opened(Token);
        var body = JsonNode.Parse(LinearIssueEditFixture.PriorityConflict(3).Body)!.AsObject();
        _ = body["issue"]!.AsObject().Remove("write");
        _f.Server.QueueLinearChange(new RouteAnswer(HttpStatusCode.Conflict, body.ToJsonString()));
        _f.Pick("Priority", "High");

        await sheet.Save(Token);

        Assert.Equal((true, false), (sheet.HasConflict, sheet.CanEdit));
    }
}
