using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context.Whole;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>#1724 P2c: the "Add several pages" sheet reads a pasted list as one row per page and writes every ticked row in one profile write (pages spec §3.6).</summary>
[Lane(TestLane.Unit)]
public sealed class AddSeveralPagesTests
{
    private const string Hash = "hash-preview";

    private static PagesPreviewBlock Block(int number, string name, int? sameAs = null, string? reason = null, bool ticked = true, PagesPreviewMatch? match = null, IReadOnlyList<PagesPreviewAction>? actions = null) =>
        new(number, number, name, match, Replaces: null, sameAs, ticked && reason is null, reason, actions ?? [], [],
            ticked && reason is null ? new PagesPreviewPage(Id: null, name, "web", Surface: null, [new PagesPreviewPageAction(Id: null, "Submit", Action: null, ["self-scheduling"])]) : null);

    private static (AddSeveralPagesViewModel Sheet, FakePageWrites Writes) Sheet(params PagesPreviewBlock[] blocks) =>
        Sheet(new PagesPreviewRead(new PagesPreview("v41", Hash, blocks)));

    private static (AddSeveralPagesViewModel Sheet, FakePageWrites Writes) Sheet(PagesPreviewRead answer)
    {
        var writes = new FakePageWrites { PreviewAnswer = answer };
        return (new AddSeveralPagesViewModel(writes, repo: null), writes);
    }

    [Fact]
    public async Task Two_blocks_naming_the_same_page_flag_the_second()
    {
        var (sheet, _) = Sheet(Block(1, "Schedule week"), Block(2, "Schedule week", sameAs: 1, reason: "Not added: it names the same page as block 1"));
        sheet.Text = "Schedule week\nSubmit\n\nSchedule week\nCopy";

        await sheet.ReadList(TestContext.Current.CancellationToken);

        Expect.Equal(2, sheet.Rows.Count);
        Expect.Equal(string.Empty, sheet.Rows[0].SameAsText);
        Expect.Equal("Same page as block 1", sheet.Rows[1].SameAsText);
        Expect.True(sheet.Rows[0].Ticked, "the first block starts ticked");
        Expect.True(!sheet.Rows[1].Ticked, "the second block starts unticked");
    }

    [Fact]
    public async Task Nothing_pasted_says_so_and_asks_nothing()
    {
        var (sheet, writes) = Sheet(Block(1, "Home"));
        sheet.Text = "  \n ";

        await sheet.ReadList(TestContext.Current.CancellationToken);

        Expect.Equal("Paste a list first: one page per block.", sheet.TextNote);
        Expect.Equal(0, writes.Previews.Count);
        Expect.Equal(0, sheet.Rows.Count);
    }

    [Fact]
    public async Task A_matched_block_reads_its_match_and_its_matched_actions()
    {
        var match = new PagesPreviewMatch("surface:route/GET:/schedule/week", "schedule-week", "Schedule week", 4);
        var actions = new[] { new PagesPreviewAction("Submit", new PagesPreviewActionMatch("action:submit", "Submit schedule"), []), new PagesPreviewAction("Print", Match: null, []) };
        var (sheet, _) = Sheet(Block(1, "Schedule week", match: match, actions: actions), Block(2, "Billing"));
        sheet.Text = "Schedule week";

        await sheet.ReadList(TestContext.Current.CancellationToken);

        Expect.Equal("Matches Schedule week (4 actions found)", sheet.Rows[0].MatchText);
        Expect.Equal("New page", sheet.Rows[1].MatchText);
        Expect.Sequence(["Submit - Matches Submit schedule", "Print"], sheet.Rows[0].ActionLines);
    }

    [Fact]
    public async Task A_row_with_a_reason_starts_unticked_and_cannot_be_ticked()
    {
        var (sheet, _) = Sheet(Block(1, "Billing", reason: "No concept called \"aproval\": pick one"));
        sheet.Text = "Billing";
        await sheet.ReadList(TestContext.Current.CancellationToken);

        sheet.Rows[0].Ticked = true;

        Expect.True(!sheet.Rows[0].Ticked, "the server's reason keeps the row off");
        Expect.Equal("No concept called \"aproval\": pick one", sheet.Rows[0].Reason);
        Expect.True(!sheet.CanSave, "nothing is ticked");
    }

    [Fact]
    public async Task Save_all_sends_one_write_with_only_the_ticked_pages_and_the_preview_hash()
    {
        var (sheet, writes) = Sheet(Block(1, "Schedule week"), Block(2, "Billing"), Block(3, "Time off"));
        sheet.Text = "x";
        await sheet.ReadList(TestContext.Current.CancellationToken);
        sheet.Rows[1].Ticked = false;
        var closed = false;
        var saved = false;
        sheet.Closed += (_, _) => closed = true;
        sheet.Saved += (_, _) => saved = true;

        Expect.Equal("Save all and approve", sheet.SaveText);
        await sheet.SaveAll(TestContext.Current.CancellationToken);

        var call = Assert.Single(writes.Saves);
        Expect.Sequence(["Schedule week", "Time off"], call.Save.Set.Select(p => p.Name));
        Expect.Equal(Hash, call.IfHash);
        Expect.True(call.Approve, "nothing else waits, so one click approves");
        Expect.True(saved && closed, "a saved list reads the section again and closes the sheet");
    }

    [Fact]
    public async Task Save_all_only_saves_when_other_changes_wait()
    {
        var (sheet, writes) = Sheet(Block(1, "Schedule week"));
        writes.Profile = new ProfileWaiting("h", ["repos.web.index.pages.other"]);
        sheet.Text = "x";
        await sheet.ReadList(TestContext.Current.CancellationToken);

        Expect.Equal("Save all", sheet.SaveText);
        await sheet.SaveAll(TestContext.Current.CancellationToken);
        Expect.True(!writes.Saves[0].Approve, "another change waits: no approval");
    }

    [Fact]
    public async Task A_clash_reads_the_list_again_and_the_next_save_uses_the_new_hash()
    {
        var (sheet, writes) = Sheet(Block(1, "Schedule week"), Block(2, "Billing"));
        sheet.Text = "the paste";
        await sheet.ReadList(TestContext.Current.CancellationToken);
        sheet.Rows[0].Ticked = false;
        var replaced = new PagesPreviewBlock(2, 5, "Billing", Match: null, Replaces: "billing", SameAs: null, Ticked: true, Reason: null, [], [], new PagesPreviewPage("billing", "Billing", "web", Surface: null, []));
        writes.AnswerPreview(new PagesPreviewRead(new PagesPreview("v42", "hash-2", [Block(1, "Schedule week"), replaced])));
        writes.Answer(new PageSaved(PageSavedState.Clash, "SND-PROFILE-003"));
        var closed = false;
        sheet.Closed += (_, _) => closed = true;

        await sheet.SaveAll(TestContext.Current.CancellationToken);

        Expect.Equal("The profile changed since you read the list. Your ticks and names are kept: Save again to apply them on top.", sheet.Note);
        Expect.True(!closed, "a clash leaves the sheet open");
        Expect.Equal("the paste", sheet.Text);
        Expect.Equal("Replaces a page you already named", sheet.Rows[1].ReplacesText);
        Expect.Sequence([false, true], sheet.Rows.Select(r => r.Ticked));
        await sheet.SaveAll(TestContext.Current.CancellationToken);
        Expect.Equal("hash-2", writes.Saves[1].IfHash);
        Expect.True(closed, "the second save lands");
    }

    [Fact]
    public async Task Editing_the_text_after_reading_turns_save_off()
    {
        var (sheet, _) = Sheet(Block(1, "Schedule week"));
        sheet.Text = "Schedule week";
        await sheet.ReadList(TestContext.Current.CancellationToken);
        Expect.True(sheet.CanSave, "the rows were read from this text");

        sheet.Text = "Schedule week\nSubmit";

        Expect.True(!sheet.CanSave, "the rows are stale until Read the list runs again");
    }

    [Fact]
    public async Task A_paste_the_server_refuses_shows_its_sentence_under_the_box()
    {
        const string Sentence = "pages preview: nothing pasted (fix: one page per block: its name, then one action per line)";
        var (sheet, _) = Sheet(new PagesPreviewRead(Preview: null, "SND-CONTEXT-259", Sentence));
        sheet.Text = "x";

        await sheet.ReadList(TestContext.Current.CancellationToken);

        Expect.Equal(Sentence, sheet.TextNote);
        Expect.Equal(0, sheet.Rows.Count);

        var (down, _) = Sheet(new PagesPreviewRead(Preview: null, "SND-API-001"));
        down.Text = "x";
        await down.ReadList(TestContext.Current.CancellationToken);
        Expect.Equal("Couldn't read the list just now. Try again.", down.TextNote);
    }

    [Fact]
    public async Task Focus_goes_to_the_first_row_with_a_reason_else_to_Save_all()
    {
        var (problem, _) = Sheet(Block(1, "Schedule week"), Block(2, "Billing", reason: "Not added: no concept"));
        var (clean, _) = Sheet(Block(1, "Schedule week"));
        var asked = new List<AddSeveralPagesFocus>();
        problem.FocusRequested += (_, e) => asked.Add(e.Value);
        clean.FocusRequested += (_, e) => asked.Add(e.Value);
        problem.Text = clean.Text = "x";

        await problem.ReadList(TestContext.Current.CancellationToken);
        await clean.ReadList(TestContext.Current.CancellationToken);

        Expect.Sequence([AddSeveralPagesFocus.ProblemRow, AddSeveralPagesFocus.SaveAll], asked);
        Expect.Equal(1, problem.ProblemRow);
    }

    [Fact]
    public void Esc_with_changes_asks_to_discard()
    {
        var (sheet, _) = Sheet(Block(1, "Schedule week"));
        var closed = 0;
        sheet.Closed += (_, _) => closed++;

        _ = sheet.Escape();
        Expect.Equal(1, closed);   // nothing pasted yet: Esc just closes

        sheet.Text = "Schedule week";
        _ = sheet.Escape();
        Expect.True(sheet.IsConfirmingDiscard && closed == 1, "a paste is a change: Esc asks first");
        Expect.Equal("Discard your changes?", sheet.DiscardQuestion);
        _ = sheet.Escape();
        Expect.True(!sheet.IsConfirmingDiscard && closed == 1, "Esc on the question is Keep editing");
    }

    [Fact]
    public void The_entry_button_opens_the_sheet_in_the_shell_and_it_goes_when_closed()
    {
        var shell = new ShellViewModel(new InMemoryEndpointRecords(), new FakeSindriApi(), new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());

        shell.Context.OpenAddSeveralPages(noConcept: false);

        Expect.True(shell.IsAddSeveralPages, "the shell's one sheet shows the Add several pages sheet");
        Expect.Equal("Add several pages", shell.ActiveAddSeveralPages?.Title);
        shell.ActiveAddSeveralPages?.Close();
        Expect.True(!shell.IsAddSeveralPages && shell.ActiveAddSeveralPages is null, "closing it lets the sheet go");
    }

    [Fact]
    public async Task A_keystroke_during_the_read_leaves_save_off()
    {
        var (sheet, writes) = Sheet(Block(1, "Schedule week"));
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        writes.PreviewGate = gate;
        sheet.Text = "Schedule week";

        var reading = sheet.ReadList(TestContext.Current.CancellationToken);
        sheet.Text = "Schedule week\nSubmit";   // typed while the read runs
        gate.SetResult();
        await reading;

        Expect.Equal("Schedule week", writes.Previews[0]);
        Expect.True(!sheet.CanSave, "the rows were read from text that is no longer in the box");
    }
}
