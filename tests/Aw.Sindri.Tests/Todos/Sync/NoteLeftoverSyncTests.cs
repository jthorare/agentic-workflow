using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Todos.Sync;

/// <summary>Writing step 5 (todo spec §4.5): a tmp file an earlier run left beside the note.</summary>
public sealed class NoteLeftoverSyncTests
{
    private static readonly string _tmp = FakeTodoNoteFiles.TmpOf(SyncRig.Path);

    private static async Task<SyncRig> Synced()
    {
        var rig = await SyncRig.Start();
        await rig.Harness.Seed(TodoHarness.First, "reply to Sam");
        _ = await rig.Pass();
        return rig;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_leftover_with_the_hash_of_the_intent_never_swapped_and_is_deleted_unread()
    {
        var rig = await Synced();
        rig.Files.Files[_tmp] = "an older note\n";
        rig.Files.Intent = FakeTodoNoteFiles.HashOf("an older note\n");
        var head = (await rig.Harness.Slice.Store.Head(TestContext.Current.CancellationToken)).Value;

        var status = await rig.Pass();

        Assert.False(rig.Files.Files.ContainsKey(_tmp));
        Assert.Equal((head, TodoNoteState.Syncing), ((await rig.Harness.Slice.Store.Head(TestContext.Current.CancellationToken)).Value, status.State));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_leftover_with_no_intent_file_is_deleted()
    {
        var rig = await Synced();
        rig.Files.Files[_tmp] = "an older note\n";
        rig.Files.Intent = null;

        _ = await rig.Pass();

        Assert.False(rig.Files.Files.ContainsKey(_tmp));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_leftover_that_differs_from_the_intent_is_what_came_out_so_its_edits_become_events_and_it_is_deleted()
    {
        var rig = await Synced();
        rig.Files.Files[_tmp] = rig.Note!.Replace("reply to Sam", "reply to Sam on Friday", StringComparison.Ordinal);
        rig.Files.Intent = FakeTodoNoteFiles.HashOf("what Sindri meant to write");

        var status = await rig.Pass();

        var changed = Assert.Single(await rig.Payloads<TodoChanged>(EventTypes.TodoChanged));
        Assert.Equal((TodoHarness.First, "reply to Sam on Friday", "obsidian", TodoNoteState.Syncing), (changed.Id, changed.Text, changed.By, status.State));
        Assert.False(rig.Files.Files.ContainsKey(_tmp));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_empty_leftover_is_kept_beside_the_note_and_stops_the_pass_as_cant_read()
    {
        var rig = await Synced();
        rig.Files.Files[_tmp] = string.Empty;
        rig.Files.Intent = FakeTodoNoteFiles.HashOf("what Sindri meant to write");
        var swaps = rig.Files.Swaps;

        var status = await rig.Pass();

        Assert.Equal((TodoNoteState.CantRead, swaps), (status.State, rig.Files.Swaps));
        Assert.Contains("empty", status.LastError, StringComparison.Ordinal);
        Assert.False(rig.Files.Files.ContainsKey(_tmp));
        Assert.Contains(rig.Files.Files.Keys, static k => k.Contains(".keep-", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_leftover_that_cannot_be_read_is_cant_read_and_nothing_is_written()
    {
        var rig = await Synced();
        rig.Files.Files[_tmp] = "x";
        rig.Files.Problems[_tmp] = "permission denied";
        var swaps = rig.Files.Swaps;

        var status = await rig.Pass();

        Assert.Equal((TodoNoteState.CantRead, swaps), (status.State, rig.Files.Swaps));
        Assert.Contains("permission denied", status.LastError, StringComparison.Ordinal);
    }
}
