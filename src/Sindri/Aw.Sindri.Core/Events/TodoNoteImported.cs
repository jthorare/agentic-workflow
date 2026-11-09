using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'todo.note.imported' payload (todo spec §4.1, REQ-E6): the owner confirmed the first import.</summary>
/// <param name="Vault">The vault's name.</param>
/// <param name="Note">The note's vault-relative path.</param>
/// <param name="Count">How many open lines were adopted.</param>
/// <param name="By">The caller.</param>
/// <param name="Via">'pipe' when the CLI had no terminal on stdout, else null.</param>
public sealed record TodoNoteImported(string Vault, string Note, int Count, string By, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Via = null);
