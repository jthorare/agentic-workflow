using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ImportTodoNote;

/// <summary>Import the todo note's open lines, or count them (POST /sindri/todos/note/import, `sindri todo note import`, `sindri_todo noteImport`; todo spec §4.4, §4.5 "First sync and import", REQ-E6): request class.</summary>
/// <param name="Vault">The vault the caller saw; SND-TODO-002 when the profile's key names another note.</param>
/// <param name="Note">The note the caller saw.</param>
/// <param name="ExpectOpen">The importable open count the caller saw; SND-TODO-409 when the note has another. Null: a dry run, which writes nothing.</param>
/// <param name="Piped">Whether the request came with fence=1: the event records 'via: pipe'.</param>
public sealed record ImportTodoNoteRequest(string Vault, string Note, int? ExpectOpen, bool Piped = false) : ICommand<ImportTodoNoteResponse>;
