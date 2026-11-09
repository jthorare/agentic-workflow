using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Todos;

namespace Aw.Sindri.Features.ImportTodoNote;

/// <summary>
/// Adopts the note's open lines (todo spec REQ-E6; request class): the caller must be known, then <see cref="ITodoNoteSync.Import"/> re-reads the note and refuses SND-TODO-002 when the profile's key doesn't name it,
/// SND-TODO-008 when it has nothing to import and SND-TODO-409 when its count isn't the one the caller saw. A request with no count is a dry run and writes nothing.
/// </summary>
/// <param name="sync">The note sync.</param>
/// <param name="callers">The caller, for the event's 'by'.</param>
internal sealed class ImportTodoNoteService(ITodoNoteSync sync, ICallerContextAccessor callers) : IService<ImportTodoNoteRequest, ImportTodoNoteResponse>
{
    public async ValueTask<Result<ImportTodoNoteResponse>> Handle(ImportTodoNoteRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("importing the todo note needs a caller");
        }

        var imported = await sync.Import(new TodoNoteImportRequest(request.Vault, request.Note, request.ExpectOpen, TodoWrite.By(caller), TodoWrite.Via(request.Piped)), cancellationToken).ConfigureAwait(false);
        if (imported.IsError)
        {
            return imported.Error;
        }

        var counts = imported.Value.Counts;
        return new ImportTodoNoteResponse(imported.Value.Imported, counts.CantImport.Count, counts.Open, counts.Done, counts.Other, counts.Importable, counts.CantImport, imported.Value.DryRun);
    }
}
