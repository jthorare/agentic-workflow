namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>GET /sindri/library (review page spec §6.1): the design library's state and its errors. The answer's asOf is not read.</summary>
/// <param name="Docs">The docs stored.</param>
/// <param name="WithErrors">The docs stored with an error.</param>
/// <param name="Broken">The docs with errors, each with its errors.</param>
/// <param name="NearMisses">Files that almost read as docs, each with what to fix.</param>
/// <param name="UnreadableRoots">The library folders that could not be read.</param>
/// <param name="CappedText">Sindri's words when the lists are cut short; null otherwise.</param>
/// <param name="Roots">Every folder of the library for the owner (aw, vaults, repo roots); empty for an agent session.</param>
internal sealed record LibraryView(int Docs, int WithErrors, IReadOnlyList<LibraryBrokenView> Broken, IReadOnlyList<LibraryNearMissView> NearMisses, IReadOnlyList<LibraryRootView> UnreadableRoots, string? CappedText, IReadOnlyList<LibraryFolderView> Roots);
