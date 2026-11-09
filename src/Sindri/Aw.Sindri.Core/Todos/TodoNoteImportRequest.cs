namespace Aw.Sindri.Core.Todos;

/// <summary>An import as the caller asked for it (todo spec §4.4 'todo note import').</summary>
/// <param name="Vault">The vault the caller saw; SND-TODO-002 when the profile's key names another note.</param>
/// <param name="Note">The note the caller saw.</param>
/// <param name="ExpectOpen">The importable open count the owner saw; null for a dry run.</param>
/// <param name="By">The caller, for the event.</param>
/// <param name="Via">'pipe' or null.</param>
public sealed record TodoNoteImportRequest(string Vault, string Note, int? ExpectOpen, string By, string? Via);
