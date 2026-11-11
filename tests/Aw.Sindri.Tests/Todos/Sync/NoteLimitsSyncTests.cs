using System.Globalization;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Todos;

namespace Aw.Sindri.Tests.Todos.Sync;

/// <summary>The sync's limits (todo spec §4.5): three tries for a lost version key, 200 adoptions a pass, 500 open todos on an import, and a note that can't be found.</summary>
public sealed class NoteLimitsSyncTests
{
    private static string Bare(int count) => string.Concat(Enumerable.Range(0, count).Select(static i => string.Create(CultureInfo.InvariantCulture, $"- [ ] bare line {i}\n")));

    [Fact, Trait("Lane", "unit")]
    public async Task A_version_key_that_is_always_taken_ends_busy_after_three_reads_of_the_note()
    {
        var rig = await SyncRig.Start();
        await rig.Harness.Seed(TodoHarness.First, "reply to Sam");
        _ = await rig.Pass();
        // the ledger already holds version 2 of the todo, but the view still says 1: every attempt builds the same key and loses it
        await rig.Harness.Record(EventTypes.TodoChanged, new TodoChanged(TodoHarness.First, 2, "reply to Sam by hand", Due: null, ClearDue: false, Links: null, "human:ui"), TodoWrite.VersionKey(TodoHarness.First, 2));
        rig.Files.Files[SyncRig.Path] = rig.Note!.Replace("reply to Sam", "reply to Sam later", StringComparison.Ordinal);
        var reads = rig.Files.QuietReads.Count;

        var status = await rig.Pass();

        Assert.Equal((TodoNoteState.Busy, reads + 3, 0), (status.State, rig.Files.QuietReads.Count, status.FailedInARow));
        _ = Assert.Single(await rig.Events(EventTypes.TodoChanged));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pass_adopts_200_bare_lines_and_leaves_the_rest_for_the_next_pass()
    {
        var rig = await SyncRig.Start();
        await rig.Harness.Seed(TodoHarness.First, "reply to Sam");
        _ = await rig.Pass();
        rig.Files.Files[SyncRig.Path] += Bare(250);

        var first = await rig.Pass();

        Assert.Equal((200, 50), ((await rig.Events(EventTypes.TodoAdded)).Count, first.NotAdopted));

        await rig.FoldAdded();

        var second = await rig.Pass();

        Assert.Equal((250, 0), ((await rig.Events(EventTypes.TodoAdded)).Count, second.NotAdopted));
        Assert.DoesNotContain("- [ ] bare line 249\n", rig.Note, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_import_adopts_no_more_than_500_open_lines()
    {
        var rig = await SyncRig.Start();
        rig.Files.Files[SyncRig.Path] = "# Todo\n\n" + Bare(520);

        var import = (await rig.Sync.Import(new TodoNoteImportRequest(SyncRig.Vault, SyncRig.NoteName, 520, "human:cli", Via: null), TestContext.Current.CancellationToken)).Value;

        Assert.Equal((500, 500), (import.Imported, (await rig.Events(EventTypes.TodoAdded)).Count));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_note_the_profile_cannot_resolve_fails_the_pass_with_the_reason_and_counts_failures_in_a_row()
    {
        var rig = await SyncRig.Start();
        rig.Locator.Error = SindriErrors.TodoNoteKey.With("todo.obsidian.vault: 'notes' can't be resolved to a folder");

        _ = await rig.Pass();
        var status = await rig.Pass();

        Assert.Equal((2, 0), (status.FailedInARow, rig.Files.Swaps));
        Assert.Contains("can't be resolved", status.LastError, StringComparison.Ordinal);

        rig.Locator.Error = null;
        var healed = await rig.Pass();

        Assert.Equal((0, null), (healed.FailedInARow, healed.LastError));
    }
}
