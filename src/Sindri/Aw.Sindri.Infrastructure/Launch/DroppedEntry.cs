namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>An 'agents' entry the reader dropped (launcher spec section 4): doctor shows it with its line, and a launch of that id is refused with SND-LAUNCH-011.</summary>
/// <param name="Id">The entry's id.</param>
/// <param name="Line">The 1-based line of the entry's key in the file, or 0 when it can't be found.</param>
/// <param name="Reason">What was wrong.</param>
internal sealed record DroppedEntry(string Id, int Line, string Reason);
