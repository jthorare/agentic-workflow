using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Host.Commands;

/// <summary>GET /sindri/library as 'sindri library list' reads it (PR review page spec §6.1): the counts, the docs with errors, the near misses and the folders that can't be read.</summary>
/// <param name="AsOf">How fresh the views are.</param>
/// <param name="Docs">How many docs the library has.</param>
/// <param name="WithErrors">How many of them have errors.</param>
/// <param name="Broken">The docs with errors, up to the route's cap.</param>
/// <param name="NearMisses">The files that look like design docs but aren't read.</param>
/// <param name="UnreadableRoots">The library folders that can't be read.</param>
/// <param name="CappedText">The line saying the lists were cut; null when they were not.</param>
internal sealed record LibraryListAnswer(
    AsOf AsOf,
    int Docs,
    int WithErrors,
    IReadOnlyList<LibraryBrokenRow> Broken,
    IReadOnlyList<LibraryNearMissRow> NearMisses,
    IReadOnlyList<LibraryUnreadableRow> UnreadableRoots,
    string? CappedText);
