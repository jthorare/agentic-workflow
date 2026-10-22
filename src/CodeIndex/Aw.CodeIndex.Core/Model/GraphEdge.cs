namespace Aw.CodeIndex.Core.Model;

/// <summary>An edge of graphify's graph (the 'graph_edges' table); only 'calls' edges are kept.</summary>
/// <param name="Source">The source node's key.</param>
/// <param name="Target">The target node's key.</param>
/// <param name="Relation">graphify's relation word.</param>
/// <param name="Confidence">graphify's confidence word: EXTRACTED or INFERRED.</param>
/// <param name="Line">The call site's line in the caller's file, or null (not stored; the merge reads it).</param>
public sealed record GraphEdge(string Source, string Target, string Relation, string Confidence, int? Line = null);
