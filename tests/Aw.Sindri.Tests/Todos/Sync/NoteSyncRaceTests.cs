using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Todos;

namespace Aw.Sindri.Tests.Todos.Sync;

/// <summary>TP-11 and TP-12: the write race, the empty came-out file, the busy note, the unreadable note and the hold.</summary>
public sealed class NoteSyncRaceTests
{
    private static async Task<SyncRig> WithNote()
    {
        var rig = await SyncRig.Start();
        await rig.Harness.Seed(TodoHarness.First, "reply to Sam");
        rig.Files.Files[SyncRig.Path] = "# Todo\n";
        return rig;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_edit_that_lands_between_the_read_and_the_swap_is_merged_and_kept()
    {
        var rig = await WithNote();
        var once = true;
        rig.Files.BeforeSwap = note =>
        {
            if (once)
            {
                once = false;
                rig.Files.Files[note] += "- [ ] from the phone\n";
            }
        };

        var status = await rig.Pass();

        var id = SyncRig.IdOnLine(rig.Note!, "from the phone");
        Assert.Equal((TodoNoteState.Syncing, 2), (status.State, rig.Files.Swaps));
        _ = Assert.NotNull(id);
        Assert.Contains("reply to Sam", rig.Note, StringComparison.Ordinal);
        Assert.Equal(id, Assert.Single(await rig.Payloads<TodoAdded>(EventTypes.TodoAdded)).Id);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_editor_that_writes_after_the_swap_is_seen_by_the_settle_and_nothing_is_lost()
    {
        var rig = await WithNote();
        var once = true;
        rig.Files.AfterSwap = tmp =>
        {
            if (once)
            {
                once = false;
                rig.Files.Files[tmp] += "- [ ] typed late\n";
            }
        };

        _ = await rig.Pass();

        _ = Assert.NotNull(SyncRig.IdOnLine(rig.Note!, "typed late"));
        Assert.Contains(rig.Files.QuietReads, r => r.Path == FakeName() && r.Quiet == TimeSpan.FromSeconds(2) && r.Max == TimeSpan.FromSeconds(30));
        Assert.DoesNotContain(rig.Files.Files.Keys, k => k.Contains("sindri-tmp", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_empty_came_out_file_is_kept_and_stops_the_pass_as_cant_read()
    {
        var rig = await WithNote();
        rig.Files.AfterSwap = tmp => rig.Files.Files[tmp] = string.Empty;

        var status = await rig.Pass();

        Assert.Equal((TodoNoteState.CantRead, 1), (status.State, rig.Files.Swaps));
        Assert.Contains(rig.Files.Files.Keys, k => k.Contains(".keep-", StringComparison.Ordinal));
        Assert.Empty(await rig.Events(EventTypes.TodoNoteSynced));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Three_swaps_that_each_find_a_new_edit_end_busy()
    {
        var rig = await WithNote();
        var n = 0;
        rig.Files.AfterSwap = tmp => rig.Files.Files[tmp] += string.Create(System.Globalization.CultureInfo.InvariantCulture, $"- [ ] edit {++n}\n");

        var status = await rig.Pass();

        Assert.Equal((TodoNoteState.Busy, 3), (status.State, rig.Files.Swaps));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_note_that_is_gone_while_the_base_has_lines_is_cant_read_and_nothing_is_removed()
    {
        var rig = await WithNote();
        _ = await rig.Pass();
        _ = rig.Files.Files.Remove(SyncRig.Path);

        var status = await rig.Pass();

        Assert.Equal((TodoNoteState.CantRead, 1, 1), (status.State, status.FailedInARow, rig.Files.Swaps));
        Assert.Empty(await rig.Events(EventTypes.TodoRemoved));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_note_emptied_to_zero_bytes_while_the_base_has_lines_is_cant_read()
    {
        var rig = await WithNote();
        _ = await rig.Pass();
        rig.Files.Files[SyncRig.Path] = string.Empty;

        var status = await rig.Pass();

        Assert.Equal(TodoNoteState.CantRead, status.State);
        Assert.Empty(await rig.Events(EventTypes.TodoRemoved));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Five_of_twenty_open_lines_deleted_open_a_hold_and_apply_none_of_them()
    {
        var rig = await SyncRig.Start();
        var ids = Enumerable.Range(0, 20).Select(i => TodoId.From("td_01jc5a8xq0n4w7ms2t9r3vb" + "kmnpqrstvwxyz"[i % 13] + "0" + "0123456789abcdefghjk"[i])).ToList();
        foreach (var id in ids)
        {
            await rig.Harness.Seed(id, "todo " + id.Value[^3..]);
        }

        _ = await rig.Pass();
        rig.Files.Files[SyncRig.Path] = string.Join('\n', rig.Note!.Split('\n').Where((_, i) => i is < 2 or >= 7));

        var status = await rig.Pass();

        var held = Assert.Single(await rig.Payloads<TodoNoteHeld>(EventTypes.TodoNoteHeld));
        Assert.Equal((5, TodoNoteState.Syncing), (held.Changes.Count, status.State));
        Assert.Empty(await rig.Events(EventTypes.TodoRemoved));
    }

    private static string FakeName() => Aw.Sindri.Testing.FakeTodoNoteFiles.TmpOf(SyncRig.Path);
}
