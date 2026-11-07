namespace Aw.Sindri.Core.Reviews;

/// <summary>What the library holds, whole: <see cref="LibraryPage.From"/> cuts the lists to the page's cap.</summary>
/// <param name="Docs">The design docs stored.</param>
/// <param name="Broken">Every doc stored with an error, in key order.</param>
/// <param name="NearMisses">Every file that almost reads as a doc.</param>
/// <param name="UnreadableRoots">The library folders that couldn't be read.</param>
public sealed record LibraryPageFacts(int Docs, IReadOnlyList<LibraryBrokenDoc> Broken, IReadOnlyList<LibraryNearMiss> NearMisses, IReadOnlyList<ReviewUnreadableRoot> UnreadableRoots);
