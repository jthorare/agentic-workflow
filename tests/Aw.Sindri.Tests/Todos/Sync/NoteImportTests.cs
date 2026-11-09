using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Todos;

namespace Aw.Sindri.Tests.Todos.Sync;

/// <summary>TP-13: the first sync waits for the import (REQ-E6), and the import's dry run, refusals and write.</summary>
public sealed class NoteImportTests
{
    private const string Open = "# Todo\n\n- [ ] one\n- [ ] two\n- [x] done one\n";

    private static async Task<SyncRig> WithOpenNote()
    {
        var rig = await SyncRig.Start();
        rig.Files.Files[SyncRig.Path] = Open;
        return rig;
    }

    private static TodoNoteImportRequest Ask(int? expect, string note = SyncRig.NoteName) => new(SyncRig.Vault, note, expect, "human:cli", Via: null);

    [Fact, Trait("Lane", "unit")]
    public async Task A_note_with_open_lines_and_no_import_waits_and_writes_nothing()
    {
        var rig = await WithOpenNote();
        await rig.Harness.Seed(TodoHarness.First, "reply to Sam");

        var status = await rig.Pass();

        Assert.Equal((TodoNoteState.WaitingForImport, 0, Open), (status.State, rig.Files.Swaps, rig.Note));
        Assert.Empty(await rig.Events(EventTypes.TodoNoteSynced, EventTypes.TodoAdded));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_dry_run_counts_and_writes_nothing()
    {
        var rig = await WithOpenNote();

        var import = (await rig.Sync.Import(Ask(expect: null), TestContext.Current.CancellationToken)).Value;

        Assert.Equal((2, 0, true, 0), (import.Counts.Importable, import.Imported, import.DryRun, rig.Files.Swaps));
        Assert.Empty(await rig.Events(EventTypes.TodoNoteImported));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_import_with_a_count_the_owner_did_not_see_is_409()
    {
        var rig = await WithOpenNote();

        var refused = await rig.Sync.Import(Ask(expect: 3), TestContext.Current.CancellationToken);

        Assert.Equal(SindriErrors.TodoConflict.Code, refused.Error.Code);
        Assert.Equal(0, rig.Files.Swaps);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_import_of_a_note_with_no_open_lines_is_008()
    {
        var rig = await SyncRig.Start();
        rig.Files.Files[SyncRig.Path] = "# Todo\n\n- [x] done one\n";

        var refused = await rig.Sync.Import(Ask(expect: 1), TestContext.Current.CancellationToken);

        Assert.Equal(SindriErrors.TodoNothingToImport.Code, refused.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_import_naming_another_note_is_002()
    {
        var rig = await WithOpenNote();

        var refused = await rig.Sync.Import(Ask(expect: 2, note: "Other.md"), TestContext.Current.CancellationToken);

        Assert.Equal(SindriErrors.TodoNoteKey.Code, refused.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_import_adopts_the_open_lines_writes_their_ids_and_records_the_count()
    {
        var rig = await WithOpenNote();

        var import = (await rig.Sync.Import(Ask(expect: 2), TestContext.Current.CancellationToken)).Value;

        Assert.Equal((2, false), (import.Imported, import.DryRun));
        _ = Assert.NotNull(SyncRig.IdOnLine(rig.Note!, "one"));
        _ = Assert.NotNull(SyncRig.IdOnLine(rig.Note!, "two"));
        var imported = Assert.Single(await rig.Payloads<TodoNoteImported>(EventTypes.TodoNoteImported));
        Assert.Equal((SyncRig.Vault, SyncRig.NoteName, 2, "human:cli"), (imported.Vault, imported.Note, imported.Count, imported.By));
        Assert.Equal(TodoNoteState.Syncing, (await rig.Pass()).State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_note_that_grows_between_the_count_and_the_adopting_pass_is_reported_as_409()
    {
        var rig = await WithOpenNote();
        var once = true;
        rig.Files.AfterRead = path =>
        {
            if (once && path == SyncRig.Path)
            {
                once = false;
                rig.Files.Files[path] += "- [ ] three\n";
            }
        };

        var refused = await rig.Sync.Import(Ask(expect: 2), TestContext.Current.CancellationToken);

        Assert.Equal(SindriErrors.TodoConflict.Code, refused.Error.Code);
        Assert.Empty(await rig.Events(EventTypes.TodoNoteImported));
    }
}
