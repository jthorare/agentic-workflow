using Aw.Kernel.Testing;
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
        var next = (string?)"unset";
        sheet.LeaveConfirmed += (_, e) => next = e.Value;
        _f.Pick("Priority", "High");

        Assert.True(sheet.Leaving(next: null));
        sheet.Discard();

        Assert.Equal((false, false, "Urgent", null), (sheet.IsConfirmingDiscard, sheet.HasChanges, _f.Field("Priority").Selected?.Name, next));
    }

    [Fact]
    public async Task Opening_another_issue_with_edits_asks_first_and_goes_there_on_discard()
    {
        var sheet = await _f.Opened(Token);
        var next = (string?)null;
        sheet.LeaveConfirmed += (_, e) => next = e.Value;
        _f.Pick("Priority", "High");

        var asked = sheet.Leaving("ENG-99");
        sheet.Discard();

        Assert.Equal((true, "ENG-99"), (asked, next));
    }

    [Fact]
    public async Task Opening_another_issue_with_nothing_changed_does_not_ask()
    {
        var sheet = await _f.Opened(Token);

        Assert.Equal((false, false), (sheet.Leaving("ENG-99"), sheet.IsConfirmingDiscard));
    }

    [Fact]
    public async Task Typed_title_text_counts_as_unsaved()
    {
        var sheet = await _f.Opened(Token);
        sheet.TitleField.Text = "Retry budget";

        Assert.True(sheet.Escape());
        Assert.Equal("Your edits to Title aren't saved yet.", sheet.DiscardBody);
    }
}
