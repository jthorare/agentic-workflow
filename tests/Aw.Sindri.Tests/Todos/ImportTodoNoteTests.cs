using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Features.ImportTodoNote;
using Aw.Sindri.Tests.Todos.Sync;

namespace Aw.Sindri.Tests.Todos;

/// <summary>The import slice over the real note sync (todo spec §4.4 'todo note import', REQ-E6, TP-13): the counts, a dry run, a stale count, a key that doesn't name the note, and who the event says asked.</summary>
public sealed class ImportTodoNoteTests
{
    private const string Open = "# Todo\n\n- [ ] one\n- [ ] two\n- [ ] see [docs](https://example.com)\n- [x] done one\n";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<(SyncRig Rig, ImportTodoNoteService Service)> Start()
    {
        var rig = await SyncRig.Start();
        rig.Files.Files[SyncRig.Path] = Open;
        return (rig, new ImportTodoNoteService(rig.Sync, rig.Harness.Slice.Callers));
    }

    private static ImportTodoNoteRequest Ask(int? expect, string note = SyncRig.NoteName, bool piped = false) => new(SyncRig.Vault, note, expect, piped);

    [Fact, Trait("Lane", "unit")]
    public async Task An_import_with_the_count_adopts_the_open_lines_and_answers_what_it_found()
    {
        var (rig, service) = await Start();
        using var cli = rig.Harness.Cli();

        var answer = (await service.Handle(Ask(expect: 2), Token)).Value;

        Assert.Equal((2, 1, 3, 1, 2, 2, false), (answer.Imported, answer.CantImport, answer.Open, answer.Done, answer.Other, answer.Importable, answer.DryRun));
        _ = Assert.Single(answer.CantImportLines);
        _ = Assert.NotNull(SyncRig.IdOnLine(rig.Note!, "one"));
        var imported = Assert.Single(await rig.Payloads<TodoNoteImported>(EventTypes.TodoNoteImported));
        Assert.Equal((2, "human:cli"), (imported.Count, imported.By));
        Assert.Null(imported.Via);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_import_whose_note_grew_after_the_check_records_the_lines_it_took_and_warns()
    {
        var (rig, service) = await Start();
        using var cli = rig.Harness.Cli();
        rig.Files.AfterRead = path =>
        {
            rig.Files.AfterRead = null;   // once: a line typed between the import's count and the adopting pass's read
            rig.Files.Files[path] += "- [ ] three\n";
        };

        var answer = await service.Handle(Ask(expect: 2), Token);

        Assert.False(answer.IsError, answer.IsError ? answer.Error.Message : null);
        Assert.Equal(3, answer.Value.Imported);
        Assert.Equal("The note had 2 lines to import when you checked and 3 when they were taken.", answer.Value.Warning);
        var imported = Assert.Single(await rig.Payloads<TodoNoteImported>(EventTypes.TodoNoteImported));
        Assert.Equal(3, imported.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_dry_run_counts_and_writes_nothing()
    {
        var (rig, service) = await Start();
        using var cli = rig.Harness.Cli();

        var answer = (await service.Handle(Ask(expect: null), Token)).Value;

        Assert.Equal((0, 2, true, 0, Open), (answer.Imported, answer.Importable, answer.DryRun, rig.Files.Swaps, rig.Note));
        Assert.Empty(await rig.Events(EventTypes.TodoNoteImported));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_count_the_note_no_longer_has_is_409_and_nothing_is_written()
    {
        var (rig, service) = await Start();
        using var cli = rig.Harness.Cli();

        var refused = await service.Handle(Ask(expect: 3), Token);

        Assert.Equal((SindriErrors.TodoConflict.Code, 0), (refused.Error.Code, rig.Files.Swaps));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_key_that_does_not_name_the_note_is_002()
    {
        var (rig, service) = await Start();
        using var cli = rig.Harness.Cli();

        var refused = await service.Handle(Ask(expect: 2, note: "Other.md"), Token);

        Assert.Equal(SindriErrors.TodoNoteKey.Code, refused.Error.Code);
        Assert.Equal(0, rig.Files.Swaps);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_piped_session_import_records_who_asked_and_via_pipe()
    {
        var (rig, service) = await Start();
        using var session = rig.Harness.Session();

        _ = (await service.Handle(Ask(expect: 2, piped: true), Token)).Value;

        var imported = Assert.Single(await rig.Payloads<TodoNoteImported>(EventTypes.TodoNoteImported));
        Assert.StartsWith("session:", imported.By, StringComparison.Ordinal);
        Assert.Equal("pipe", imported.Via);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_request_with_no_caller_is_refused()
    {
        var (_, service) = await Start();

        var refused = await service.Handle(Ask(expect: 2), Token);

        Assert.Equal(SindriErrors.AuthRefused.Code, refused.Error.Code);
    }
}
