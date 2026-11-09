namespace Aw.Sindri.Features.GetTodoNote;

/// <summary>A note line that can't sync (todo spec §4.5).</summary>
/// <param name="Line">The 1-based line number.</param>
/// <param name="Reason">Why, in plain words.</param>
public sealed record TodoNoteCantSync(int Line, string Reason);
