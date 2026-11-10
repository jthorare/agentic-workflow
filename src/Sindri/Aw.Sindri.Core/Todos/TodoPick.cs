namespace Aw.Sindri.Core.Todos;

/// <summary>A conflict pick still inside its Undo window (todo spec D11): what the todo was before the pick, and the version the pick left it at.</summary>
/// <param name="At">When the pick was made.</param>
/// <param name="Prior">The todo's row before the pick, with its conflict still open.</param>
/// <param name="VersionAfter">The version the todo was at when the pick finished (Undo is refused when it moved since).</param>
/// <param name="Kept">'sindri' or 'obsidian': the side the pick kept; empty when not recorded.</param>
/// <param name="By">Who picked ('human:ui', 'session:…'); empty when not recorded.</param>
public sealed record TodoPick(DateTimeOffset At, TodoRow Prior, long VersionAfter, string Kept = "", string By = "");
