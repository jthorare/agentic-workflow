using System.Text.Json.Serialization;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'todo.added' payload (todo spec §4.1): a todo was added by the owner, a session, the CLI or the note.</summary>
/// <param name="Id">The todo's id.</param>
/// <param name="Text">The todo's text, scrubbed by Aw.Redaction.</param>
/// <param name="Due">The due date, or null.</param>
/// <param name="Links">The links, as deep links, at most 10.</param>
/// <param name="ClientId">The caller's retry id (1 to 64 of A-Za-z0-9_-), or null for a todo adopted from the note.</param>
/// <param name="By">Who wrote it: the caller's actor with its launch id, 'service:todo-note' or 'obsidian'.</param>
/// <param name="Via">'pipe' when the CLI had no terminal on stdout, else null.</param>
public sealed record TodoAdded(TodoId Id, string Text, DateOnly? Due, IReadOnlyList<string> Links, TodoClientId? ClientId, string By, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Via = null);
