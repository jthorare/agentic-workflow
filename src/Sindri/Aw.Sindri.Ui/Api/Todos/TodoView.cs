namespace Aw.Sindri.Ui.Api.Todos;

/// <summary>One todo as GET /sindri/todos serves it (todo spec §4.2).</summary>
/// <param name="Id">"td_…".</param>
/// <param name="Text">The text (fenced for a session caller; the app is not one).</param>
/// <param name="Due">The due day, or null.</param>
/// <param name="Links">0 to 10 links.</param>
/// <param name="AddedAt">When it was added.</param>
/// <param name="AddedBy">Who added it: "human:ui", "human:cli", "session:…", "obsidian".</param>
/// <param name="AddedVia">"pipe" when a CLI with no terminal added it, else null.</param>
/// <param name="DoneAt">When it was ticked; null while open.</param>
/// <param name="Removed">True once removed.</param>
/// <param name="RemovedAt">When it was removed, or null.</param>
/// <param name="RemovedBy">Who removed it, or null.</param>
/// <param name="Version">The version a write sends back.</param>
/// <param name="LastBy">Who last changed it.</param>
/// <param name="LastVia">"pipe" when that change came from a piped CLI, else null.</param>
/// <param name="Conflict">The open both-places conflict, or null.</param>
internal sealed record TodoView(
    string Id,
    string Text,
    DateOnly? Due,
    IReadOnlyList<TodoLinkView> Links,
    DateTimeOffset AddedAt,
    string AddedBy,
    string? AddedVia,
    DateTimeOffset? DoneAt,
    bool Removed,
    DateTimeOffset? RemovedAt,
    string? RemovedBy,
    long Version,
    string LastBy,
    string? LastVia,
    TodoConflictView? Conflict);
