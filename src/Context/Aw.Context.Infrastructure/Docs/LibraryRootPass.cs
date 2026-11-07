using Aw.Context.Core.Library;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;

namespace Aw.Context.Infrastructure.Docs;

/// <summary>What one library root's turn in a pull came to.</summary>
/// <param name="Root">The root's name.</param>
/// <param name="Path">The root's folder.</param>
/// <param name="Problem">Why the root could not be listed, or null.</param>
/// <param name="Fingerprint">The listing's fingerprint to remember, or null when the next pull has to diff again (a failed root, or a page cut short by the limit).</param>
/// <param name="Unchanged">True when the listing is the one the cursor already holds: nothing was read, and the docs and near misses stay as the last report said.</param>
/// <param name="Docs">The design docs found.</param>
/// <param name="NearMisses">The files that look like design docs but aren't read, each with its root-relative path and its SND-LIB-019 error.</param>
/// <param name="Items">The docs that are new or changed.</param>
/// <param name="Removed">The stored docs the root no longer holds.</param>
internal sealed record LibraryRootPass(
    string Root,
    string Path,
    string? Problem,
    string? Fingerprint,
    bool Unchanged,
    int Docs,
    IReadOnlyList<DesignDocFinding> NearMisses,
    IReadOnlyList<RecordItem> Items,
    IReadOnlyList<RecordKey> Removed)
{
    internal static LibraryRootPass Failed(string root, string path, string problem) => new(root, path, problem, Fingerprint: null, Unchanged: false, 0, [], [], []);

    internal static LibraryRootPass Same(string root, string path, string fingerprint) => new(root, path, Problem: null, fingerprint, Unchanged: true, 0, [], [], []);
}
