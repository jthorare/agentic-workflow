using System.Globalization;
using System.Text;
using Aw.Sindri.Core;
using Aw.Sindri.Features.ImportTodoNote;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host.Commands;

/// <summary>'sindri todo note import [--expect N] [--dry-run]' (todo spec §4.4, §4.5 "First sync and import", REQ-E6): counts the note's open lines, or adopts them when the count is the one the owner saw.</summary>
internal sealed partial class TodoVerbs
{
    private async Task<int> ImportNote(TodoVerbArguments.Parsed command, CancellationToken ct)
    {
        var target = noteKey is null
            ? SindriErrors.TodoNoteKey.With("todo.obsidian isn't set (fix: sindri todo note set <vault> <note>)")
            : await noteKey(ct).ConfigureAwait(false);
        if (target.IsError)
        {
            return await output.Error(target.Error).ConfigureAwait(false);
        }

        var fence = Fenced(command) ? "?fence=1" : string.Empty;
        var body = new ImportTodoNoteBody(target.Value.Vault, target.Value.Note, command.Expect, command.DryRun);
        var sent = await client.Post<ImportTodoNoteBody, ImportTodoNoteResponse>(Root + "/note/import" + fence, body, ct).ConfigureAwait(false);
        if (sent.IsError)
        {
            return await output.Error(sent.Error).ConfigureAwait(false);
        }

        return command.Json
            ? await output.Raw(sent.Value.Body, 0).ConfigureAwait(false)
            : await output.Text(RenderImport(sent.Value.Value), 0).ConfigureAwait(false);
    }

    private static string RenderImport(ImportTodoNoteResponse import)
    {
        var text = new StringBuilder();
        _ = text.Append(CultureInfo.InvariantCulture, $"{import.Open} open, {import.Done} done, {import.Other} other lines; {import.Importable} can be imported, {import.CantImport} can't\n");
        foreach (var line in import.CantImportLines)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"  line {line.Line}: {line.Reason}\n");
        }

        _ = import.DryRun
            ? text.Append(CultureInfo.InvariantCulture, $"dry run, nothing written; import with: sindri todo note import --expect {import.Importable}\n")
            : text.Append(CultureInfo.InvariantCulture, $"imported {import.Imported} lines; {import.CantImport} stay as text\n");
        return text.ToString();
    }
}
