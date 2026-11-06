namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>The Reviews header's library state (review page spec §6.1, §16); its count opens the library errors sheet.</summary>
/// <param name="State">"ok", "reading", "empty" or "error".</param>
/// <param name="Docs">The design docs stored.</param>
/// <param name="WithErrors">The docs stored with an error.</param>
/// <param name="UnreadableRoots">The library folders that couldn't be read.</param>
/// <param name="Text">Sindri's words ("Library: 33 docs, 1 folder can't be read").</param>
internal sealed record ReviewLibraryView(string State, int Docs, int WithErrors, int UnreadableRoots, string Text);
