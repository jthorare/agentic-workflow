using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Todos;

namespace Aw.Sindri.Tests.Todos.Sync;

/// <summary>TP-9: the note sync's ordinary passes (todo spec §4.5).</summary>
public sealed class NoteSyncWriteTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task A_todo_added_in_Sindri_is_written_to_the_note_with_its_id()
    {
        var rig = await SyncRig.Start();
        await rig.Harness.Seed(TodoHarness.First, "reply to Sam");

        var status = await rig.Pass();

        Assert.Equal(TodoNoteState.Syncing, status.State);
        Assert.Equal("# Todo\n\n- [ ] reply to Sam ^td-01jc5a8xq0n4w7ms2t9r3vbk6e\n", rig.Note);
        _ = Assert.Single(await rig.Events(EventTypes.TodoNoteSynced));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_line_ticked_in_the_note_is_done_in_Sindri_by_obsidian()
    {
        var rig = await SyncRig.Start();
        await rig.Harness.Seed(TodoHarness.First, "reply to Sam");
        _ = await rig.Pass();
        rig.Files.Files[SyncRig.Path] = rig.Note!.Replace("- [ ]", "- [x]", StringComparison.Ordinal);

        _ = await rig.Pass();

        var done = Assert.Single(await rig.Payloads<TodoDone>(EventTypes.TodoDone));
        Assert.Equal((TodoHarness.First, 2L, "obsidian"), (done.Id, done.Version, done.By));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_bare_line_gets_an_id_written_and_its_todo_added_event_comes_after_the_swap()
    {
        var rig = await SyncRig.Start();
        await rig.Harness.Seed(TodoHarness.First, "reply to Sam");
        _ = await rig.Pass();
        rig.Files.Files[SyncRig.Path] += "- [ ] buy milk\n";
        var addedBeforeSwap = -1;
        rig.Files.AfterSwap = _ => addedBeforeSwap = rig.Events(EventTypes.TodoAdded).GetAwaiter().GetResult().Count;

        _ = await rig.Pass();

        var id = SyncRig.IdOnLine(rig.Note!, "buy milk");
        _ = Assert.NotNull(id);
        var added = Assert.Single(await rig.Payloads<TodoAdded>(EventTypes.TodoAdded));
        Assert.Equal((id, "obsidian", "buy milk", 0), (added.Id, added.By, added.Text, addedBeforeSwap));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_same_text_added_again_after_the_first_is_done_is_a_second_todo()
    {
        var rig = await SyncRig.Start();
        await rig.Harness.Seed(TodoHarness.First, "buy milk");
        _ = await rig.Pass();
        rig.Files.Files[SyncRig.Path] = rig.Note!.Replace("- [ ]", "- [x]", StringComparison.Ordinal) + "- [ ] buy milk\n";

        _ = await rig.Pass();

        var added = Assert.Single(await rig.Payloads<TodoAdded>(EventTypes.TodoAdded));
        Assert.NotEqual(TodoHarness.First, added.Id);
        Assert.Equal("buy milk", added.Text);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_line_copied_with_its_id_gives_the_copy_a_new_id()
    {
        var rig = await SyncRig.Start();
        await rig.Harness.Seed(TodoHarness.First, "reply to Sam");
        _ = await rig.Pass();
        rig.Files.Files[SyncRig.Path] += "- [ ] reply to Sam ^td-01jc5a8xq0n4w7ms2t9r3vbk6e\n";

        _ = await rig.Pass();

        var added = Assert.Single(await rig.Payloads<TodoAdded>(EventTypes.TodoAdded));
        Assert.NotEqual(TodoHarness.First, added.Id);
        Assert.Contains($"^td-{added.Id.Value["td_".Length..]}", rig.Note, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pass_that_changes_nothing_appends_nothing_and_writes_nothing()
    {
        var rig = await SyncRig.Start();
        await rig.Harness.Seed(TodoHarness.First, "reply to Sam");
        _ = await rig.Pass();
        var head = (await rig.Harness.Slice.Store.Head(TestContext.Current.CancellationToken)).Value;

        var status = await rig.Pass();

        Assert.Equal(head, (await rig.Harness.Slice.Store.Head(TestContext.Current.CancellationToken)).Value);
        Assert.Equal((1, TodoNoteState.Syncing, 0), (rig.Files.Swaps, status.State, status.FailedInARow));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_removed_key_is_NotSet_and_deletes_every_base()
    {
        var rig = await SyncRig.Start();
        await rig.Harness.Seed(TodoHarness.First, "reply to Sam");
        _ = await rig.Pass();
        rig.Locator.Place = null;

        var status = await rig.Pass();

        Assert.Equal(TodoNoteState.NotSet, status.State);
        Assert.Equal(0, rig.Files.BaseCount);
        Assert.Null(rig.Files.BasesDeleted[^1]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_disk_without_the_swap_is_read_one_way_and_never_written()
    {
        var rig = await SyncRig.Start();
        await rig.Harness.Seed(TodoHarness.First, "reply to Sam");
        rig.Disk.CanSwap = false;
        rig.Files.Files[SyncRig.Path] = "# Todo\n";

        var status = await rig.Pass();

        Assert.Equal((TodoNoteState.OneWay, 0, "# Todo\n"), (status.State, rig.Files.Swaps, rig.Note));
    }
}
