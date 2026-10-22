using Aw.CodeIndex.Core.Model;

namespace Aw.CodeIndex.Core.Graph;

/// <summary>What graphify's graph adds to an index (spec §5.5): the graph-symbol rows of the files no parser covers and the call edges between them.</summary>
/// <param name="Files">One record per non-C# code file of the snapshot, with the graph symbols graphify named in it (none for a file it named nothing in).</param>
/// <param name="Edges">The call edges, basis graphify.</param>
/// <param name="Dropped">Call edges that named a symbol the merge could not place.</param>
public sealed record GraphMerged(IReadOnlyList<FileRecord> Files, IReadOnlyList<EdgeRecord> Edges, int Dropped);
