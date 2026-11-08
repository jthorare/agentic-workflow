using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Todo;
using static Aw.Sindri.Ui.Tests.Todo.AttentionFixture;

namespace Aw.Sindri.Ui.Tests.Todo;

/// <summary>The Todo panel's attention rows (todo spec §3.1, §3.4, D13): one line per slot, one row open at a time, the five-row count, the changed-field marks and the removal wording.</summary>
[Lane(TestLane.Unit)]
public sealed class TodoAttentionRowsTests
{
    private readonly AttentionFixture _a = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static JsonNode[] Todos(params JsonNode[] first) => [.. first, .. Six()];

    [Fact]
    public async Task One_conflict_is_one_line_in_the_first_slot_and_leaves_four_todo_rows()
    {
        _a.Serve(TodoNoteSample.WithConflicts("td_c1"), Todos(TextOnly("td_c1", "reply to Sam about the offsite")));

        var panel = await _a.Read(Token);

        var row = Assert.IsType<AttentionRowViewModel>(panel.Attention);
        Assert.Equal((AttentionKind.Conflict, "td_c1", "Changed in Sindri and in Obsidian: reply to Sam about the offsite", "to sort it out"), (row.Kind, row.Id, row.Line, row.EnterHint));
        Assert.False(row.IsOpen);
        Assert.False(panel.HasMore);
        Assert.Equal(["td_open0", "td_open1", "td_open2", "td_open3"], panel.Rows.Select(r => r.Id), StringComparer.Ordinal);
        Assert.DoesNotContain(panel.Rows, r => r.Id == "td_c1");
    }

    [Fact]
    public async Task Three_conflicts_show_the_oldest_and_fold_the_rest_into_Sort_out_2_more()
    {
        _a.Serve(TodoNoteSample.WithConflicts("td_c2", "td_c1", "td_c3"), Todos(TextOnly("td_c1"), TextOnly("td_c2"), TextOnly("td_c3")));

        var panel = await _a.Read(Token);

        Assert.Equal("td_c2", panel.Attention!.Id);   // the note lists them oldest first
        Assert.Equal(("Sort out 2 more", "2 more to sort out", true), (panel.MoreText, panel.MoreName, panel.HasMore));
        Assert.Equal(3, panel.Rows.Count);   // two slots for the attention area, never fewer than three todo rows
    }

    [Fact]
    public async Task Enter_on_the_more_line_shows_the_next_row_and_the_one_shown_folds()
    {
        _a.Serve(TodoNoteSample.WithConflicts("td_c1", "td_c2", "td_c3"), Todos(TextOnly("td_c1"), TextOnly("td_c2"), TextOnly("td_c3")));
        var panel = await _a.Read(Token);
        Assert.True(panel.OpenAttention());

        Assert.True(panel.OpenNextAttention());

        Assert.Equal("td_c2", panel.Attention!.Id);
        Assert.False(panel.Attention.IsOpen);   // only one attention row is open at a time
        Assert.Equal("Sort out 2 more", panel.MoreText);
    }

    [Fact]
    public async Task A_conflict_and_a_hold_show_the_conflict_first_and_the_hold_behind_it()
    {
        _a.Serve(TodoNoteSample.WithHold(4, "sha256:3f1c", "td_c1"), Todos(TextOnly("td_c1")));

        var panel = await _a.Read(Token);

        Assert.Equal((AttentionKind.Conflict, "Sort out 1 more", 3), (panel.Attention!.Kind, panel.MoreText, panel.Rows.Count));
        Assert.True(panel.OpenNextAttention());
        Assert.Equal((AttentionKind.Hold, "Sort out 1 more"), (panel.Attention!.Kind, panel.MoreText));
    }

    [Fact]
    public async Task A_hold_alone_is_one_line_with_its_first_three_changes_in_words()
    {
        _a.Serve(TodoNoteSample.WithHold(4), Todos());

        var panel = await _a.Read(Token);

        var hold = Assert.IsType<AttentionRowViewModel>(panel.Attention);
        Assert.Equal(("The note changed 4 todos.", "to look", AttentionRowViewModel.HoldId, 4), (hold.Line, hold.EnterHint, hold.Id, hold.HeldCount));
        Assert.Equal("removed 'reply to Sam about the offsite', edited 'call the dentist', un-ticked 'renew the cert' and 1 more", hold.Meta);
        Assert.Equal(4, panel.Rows.Count);
        Assert.False(panel.HasMore);
    }

    [Fact]
    public async Task The_hold_summary_names_three_changes_and_counts_the_rest()
    {
        _a.Serve(TodoNoteSample.WithHold(14), Todos());

        var hold = (await _a.Read(Token)).Attention!;

        Assert.Equal("removed 'reply to Sam about the offsite', edited 'call the dentist', un-ticked 'renew the cert' and 11 more", hold.Meta);
        Assert.Equal("The note changed 14 todos.", hold.Title);
    }

    [Fact]
    public async Task An_open_hold_lists_five_changes_and_two_buttons_with_Put_them_back_on_1_and_Apply_on_2()
    {
        _a.Serve(TodoNoteSample.WithHold(14), Todos());
        var panel = await _a.Read(Token);

        Assert.True(panel.OpenAttention());

        var hold = panel.Attention!;
        Assert.True(hold.IsOpen);
        Assert.Equal(["Removed: reply to Sam about the offsite", "Edited: call the dentist", "Un-ticked: renew the cert", "Removed: held todo 3", "Removed: held todo 4"], hold.Changes);
        Assert.Equal("and 9 more", hold.MoreChanges);
        Assert.Equal([("1", "Put them back in the note", "Put the 14 todos back in the note"), ("2", "Apply in Sindri too", "Apply the 14 changes in Sindri")], hold.Choices.Select(c => (c.Key, c.ButtonText, c.ButtonName)));
    }

    [Fact]
    public async Task Enter_opens_the_row_and_asks_for_focus_on_its_first_button_and_Esc_folds_it()
    {
        _a.Serve(TodoNoteSample.WithConflicts("td_c1"), Todos(TextOnly("td_c1")));
        var panel = await _a.Read(Token);

        Assert.True(panel.OpenAttention());

        Assert.Equal((true, true, 1), (panel.IsAttentionOpen, panel.Attention!.IsOpen, _a.ButtonsAsked));
        Assert.True(panel.CloseAttention());
        Assert.Equal((false, false), (panel.IsAttentionOpen, panel.Attention!.IsOpen));
        Assert.False(panel.CloseAttention());   // nothing open: Esc goes on to fold the panel
    }

    [Fact]
    public async Task Every_changed_field_gets_its_mark_and_the_digits_are_Sindri_1_and_Obsidian_2()
    {
        var conflict = Conflicted("td_c1", "reply to Sam", Side("reply to Sam", "2026-01-02", done: true), Side("reply to Sam today", "2026-01-03", done: false, removed: false, "sindri://linear/ENG-12"));
        _a.Serve(TodoNoteSample.WithConflicts("td_c1"), Todos(conflict));
        var panel = await _a.Read(Token);
        _ = panel.OpenAttention();

        var (sindri, obsidian) = (panel.Attention!.Choices[0], panel.Attention.Choices[1]);

        Assert.Equal(("1", "Sindri", "Keep Sindri's", "Keep Sindri's version: reply to Sam"), (sindri.Key, sindri.Heading, sindri.ButtonText, sindri.ButtonName));
        Assert.Equal(("2", "Obsidian", "Keep Obsidian's", "Keep Obsidian's version: reply to Sam today"), (obsidian.Key, obsidian.Heading, obsidian.ButtonText, obsidian.ButtonName));
        Assert.Equal([("Text", "reply to Sam"), ("Due", "Fri 01-02"), ("Links", "No links"), ("State", "Done")], sindri.Fields.Select(f => (f.Label, f.Value)));
        Assert.Equal([("Text", "reply to Sam today"), ("Due", "Sat 01-03"), ("Links", "sindri://linear/ENG-12"), ("State", "Not done")], obsidian.Fields.Select(f => (f.Label, f.Value)));
        Assert.All(sindri.Fields.Concat(obsidian.Fields), f => Assert.Equal("changed", f.Mark));
        Assert.Equal(string.Empty, panel.Attention.Meta);
    }

    [Fact]
    public async Task A_field_both_versions_share_is_drawn_once_dimmed_not_in_the_columns()
    {
        _a.Serve(TodoNoteSample.WithConflicts("td_c1"), Todos(TextOnly("td_c1")));
        var panel = await _a.Read(Token);
        _ = panel.OpenAttention();

        var row = panel.Attention!;

        Assert.Equal(["Text"], row.Choices[0].Fields.Select(f => f.Label), StringComparer.Ordinal);
        Assert.Equal(["Text"], row.Choices[1].Fields.Select(f => f.Label), StringComparer.Ordinal);
        Assert.Equal("Fri 01-02 · Not done, same in both", row.Meta);
    }

    [Fact]
    public async Task A_removal_in_Obsidian_says_Removed_and_its_button_reads_Remove_it()
    {
        var conflict = Conflicted("td_c1", "reply to Sam", Side("reply to Sam"), Side(text: null, due: null, removed: true));
        _a.Serve(TodoNoteSample.WithConflicts("td_c1"), Todos(conflict));
        var panel = await _a.Read(Token);
        _ = panel.OpenAttention();

        var obsidian = panel.Attention!.Choices[1];

        Assert.True(obsidian.IsRemoved);
        Assert.Equal("Remove it", obsidian.ButtonText);
        Assert.Equal("Remove the todo: reply to Sam", obsidian.ButtonName);
        Assert.Contains(obsidian.Fields, f => string.Equals(f.Label, "State", StringComparison.Ordinal) && string.Equals(f.Value, "Removed", StringComparison.Ordinal));
        Assert.Equal("Keep Sindri's", panel.Attention.Choices[0].ButtonText);
    }

    [Fact]
    public async Task A_removal_in_Sindri_says_Removed_and_its_button_reads_Keep_it_removed()
    {
        var conflict = Conflicted("td_c1", "reply to Sam", Side(text: null, due: null, removed: true), Side("reply to Sam, edited"));
        _a.Serve(TodoNoteSample.WithConflicts("td_c1"), Todos(conflict));
        var panel = await _a.Read(Token);
        _ = panel.OpenAttention();

        var sindri = panel.Attention!.Choices[0];

        Assert.True(sindri.IsRemoved);
        Assert.Equal(("Keep it removed", "Keep Sindri's version: removed"), (sindri.ButtonText, sindri.ButtonName));
        Assert.Equal("Keep Obsidian's", panel.Attention.Choices[1].ButtonText);
    }

    [Fact]
    public async Task A_280_character_text_reaches_the_row_whole_for_the_view_to_wrap()
    {
        var long280 = new string('x', 280);
        _a.Serve(TodoNoteSample.WithConflicts("td_c1"), Todos(TextOnly("td_c1", long280, new string('y', 280))));

        var panel = await _a.Read(Token);
        _ = panel.OpenAttention();

        Assert.Equal(long280, panel.Attention!.Choices[0].Fields[0].Value);
    }

    [Fact]
    public async Task The_row_reads_as_one_sentence_for_a_screen_reader()
    {
        _a.Serve(TodoNoteSample.WithHold(14, "sha256:3f1c", "td_c1"), Todos(TextOnly("td_c1")));
        var panel = await _a.Read(Token);

        Assert.Equal("Changed in Sindri and in Obsidian: reply to Sam. Press 1 to keep Sindri's, 2 to keep Obsidian's, Enter for the buttons", panel.Attention!.AccessibleName);
        _ = panel.OpenNextAttention();
        Assert.Equal(
            "The Obsidian note changed 14 todos: removed 'reply to Sam about the offsite', edited 'call the dentist', un-ticked 'renew the cert' and 11 more. Press 1 to put them back in the note, 2 to apply them in Sindri",
            panel.Attention!.AccessibleName);
    }

    [Fact]
    public async Task The_expanded_panel_leaves_a_conflicted_todo_out_of_its_groups()
    {
        _a.Serve(TodoNoteSample.WithConflicts("td_c1"), Todos(TextOnly("td_c1")));
        var panel = await _a.Read(Token);

        panel.Expand();

        Assert.DoesNotContain(panel.Groups.SelectMany(g => g.Rows), r => r.Id == "td_c1");
        Assert.Contains("1 to sort out", panel.CountLine, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Older_Sindri_answers_a_bare_404_for_the_note_and_the_panel_draws_no_attention_row()
    {
        _a.Serve(TodoNoteSample.Quiet, Todos(TextOnly("td_c1")));
        _a.F.Server.TodoNoteAnswer = new RouteAnswer(System.Net.HttpStatusCode.NotFound, string.Empty);

        var panel = await _a.Read(Token);

        Assert.Null(panel.Attention);
        Assert.False(panel.HasAttention);
        Assert.Contains(panel.Rows, r => r.Id == "td_c1");   // no sync: the todo stays a plain row
        Assert.Equal(string.Empty, panel.Error);
    }

    [Fact]
    public async Task A_note_that_cannot_be_read_for_another_reason_draws_no_attention_row_and_no_error()
    {
        _a.Serve(TodoNoteSample.WithConflicts("td_c1"), Todos(TextOnly("td_c1")));
        _a.F.Server.TodoNoteAnswer = RouteAnswer.Problem(System.Net.HttpStatusCode.ServiceUnavailable, "SND-API-010", "rebuilding");

        var panel = await _a.Read(Token);

        Assert.Null(panel.Attention);
        Assert.Equal(string.Empty, panel.Error);
    }
}
