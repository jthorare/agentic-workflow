namespace Aw.Sindri.Core.Reviews;

/// <summary>What the library holds, for the Reviews header (PR review page spec §6.1).</summary>
/// <param name="Reading">Whether the first read is still running.</param>
/// <param name="Docs">The design docs stored.</param>
/// <param name="WithErrors">The docs stored with an error.</param>
/// <param name="UnreadableRoots">The library folders that couldn't be read, each with why.</param>
public sealed record ReviewLibraryFacts(bool Reading, int Docs, int WithErrors, IReadOnlyList<ReviewUnreadableRoot> UnreadableRoots);
