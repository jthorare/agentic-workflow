using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Linear;

namespace Aw.Sindri.Ui.Tests.Linear;

/// <summary>Leaving the issue sheet with unsaved edits (Linear hub spec §3): Esc, Back or another issue asks first; with nothing changed it just goes.</summary>
[Lane(TestLane.Unit)]
public sealed class LinearIssueEditDiscardTests
{
    private readonly LinearIssueEditFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Esc_with_unsaved_edits_asks_which_fields_are_not_saved_and_puts_focus_on_keep_editing()
    {
        var sheet = await _f.Opened(Token);
        _f.Pick("State", "Done");
        _f.Pick("Priority", "High");

        var spent = sheet.Escape();

        Assert.Equal((true, true, "Discard changes to ENG-12?", "Your edits to State and Priority aren't saved yet."), (spent, sheet.IsConfirmingDiscard, sheet.DiscardHeading, sheet.DiscardBody));
        Assert.Equal([LinearIssueFocus.KeepEditing], _f.Focus);
    }

    [Fact]
    public async Task Esc_with_nothing_changed_is_not_spent_so_the_sheet_closes()
    {
        var sheet = await _f.Opened(Token);

        Assert.Equal((false, false), (sheet.Escape(), sheet.IsConfirmingDiscard));
    }

    [Fact]
    public async Task Esc_on_the_question_keeps_editing_and_the_edits_stay()
    {
        var sheet = await _f.Opened(Token);
        _f.Pick("Priority", "High");
        _ = sheet.Escape();

        var spent = sheet.Escape();

        Assert.Equal((true, false, true), (spent, sheet.IsConfirmingDiscard, sheet.HasChanges));
    }

    [Fact]
    public async Task Discard_drops_the_edits_and_lets_the_leaving_go_ahead()
    {
        var sheet = await _f.Opened(Token);
        var left = 0;
        sheet.LeaveConfirmed += (_, _) => left++;
        _f.Pick("Priority", "High");

        Assert.True(sheet.Leaving());
        sheet.Discard();

        Assert.Equal((false, false, "Urgent", 1), (sheet.IsConfirmingDiscard, sheet.HasChanges, _f.Field("Priority").Selected?.Name, left));
    }

    [Fact]
    public async Task Leaving_with_a_way_on_of_its_own_runs_that_on_discard_instead_of_closing()
    {
        var sheet = await _f.Opened(Token);
        var (left, ran) = (0, 0);
        sheet.LeaveConfirmed += (_, _) => left++;
        _f.Pick("Priority", "High");

        var asked = sheet.Leaving(then: () => ran++);
        sheet.Discard();

        Assert.Equal((true, 1, 0), (asked, ran, left));
    }

    [Fact]
    public async Task Leaving_with_nothing_changed_does_not_ask()
    {
        var sheet = await _f.Opened(Token);

        Assert.Equal((false, false), (sheet.Leaving(), sheet.IsConfirmingDiscard));
    }

    [Fact]
    public async Task Typed_title_text_counts_as_unsaved()
    {
        var sheet = await _f.Opened(Token);
        sheet.TitleField.Text = "Retry budget";

        Assert.True(sheet.Escape());
        Assert.Equal("Your edits to Title aren't saved yet.", sheet.DiscardBody);
    }

    [Fact]
    public async Task A_save_that_applies_while_the_question_shows_clears_it_and_a_later_Discard_does_nothing()
    {
        var sheet = await _f.Opened(Token);
        var left = 0;
        sheet.LeaveConfirmed += (_, _) => left++;
        _f.Pick("State", "In Review");
        Assert.True(sheet.Leaving());

        await sheet.Save(Token);
        sheet.Discard();

        Assert.Equal((false, 0), (sheet.IsConfirmingDiscard, left));
    }

    [Fact]
    public async Task Putting_the_edits_back_while_the_question_shows_clears_it()
    {
        var sheet = await _f.Opened(Token);
        _f.Pick("Priority", "High");
        Assert.True(sheet.Leaving());

        _f.Pick("Priority", "Urgent");

        Assert.False(sheet.IsConfirmingDiscard);
    }

    [Fact]
    public async Task Discard_does_nothing_while_a_save_is_in_flight()
    {
        var sheet = await _f.Opened(Token);
        _f.Server.LinearChangeGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _f.Pick("Priority", "High");
        Assert.True(sheet.Leaving());

        var saving = sheet.Save(Token);
        sheet.Discard();

        Assert.Equal((true, true, true), (sheet.IsConfirmingDiscard, sheet.HasChanges, sheet.IsLocked));
        _f.Server.LinearChangeGate.SetResult();
        await saving;
    }

    [Fact]
    public async Task After_a_save_with_no_answer_Discard_still_lets_the_sheet_go_and_sends_no_retry()
    {
        // Sindri down or the call timed out: the outcome is unknown, which is exactly when the owner may want to drop the edits.
        var sheet = await _f.Opened(Token);
        _f.Server.QueueLinearChange(LinearIssueEditFixture.Change("unknown"));
        _f.Pick("Priority", "High");
        await sheet.Save(Token);
        var closed = false;
        sheet.LeaveConfirmed += (_, _) => closed = true;

        Assert.True(sheet.Leaving());
        sheet.Discard();

        Assert.Equal((true, false, 1), (closed, sheet.IsConfirmingDiscard, _f.Server.LinearChanges.Count));
    }

    [Fact]
    public async Task A_refresh_that_answers_not_synced_keeps_the_issue_and_the_edits()
    {
        var sheet = await _f.Opened(Token);
        _f.Pick("Priority", "High");
        _f.Server.LinearIssueAnswer = LinearIssueSheetFixture.NotFound();

        await sheet.Refresh(LinearIssueEditFixture.Url, Token);

        Assert.Equal((FramePhase.Shown, "Retry budget for the sync worker", "High", true), (sheet.Phase, sheet.Title, _f.Field("Priority").Selected?.Name, sheet.HasChanges));
    }
}
