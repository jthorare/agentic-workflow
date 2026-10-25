using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Search;

/// <summary>One node the search found.</summary>
/// <param name="Key">The node's key.</param>
/// <param name="Kind">The node's kind.</param>
/// <param name="Label">The label as the caller reads it (fenced for a caller that gets the fence when the node is untrusted).</param>
/// <param name="Repo">The repo the node's evidence points at, else the repo the answer is for, else null.</param>
/// <param name="Area">The area the node belongs to.</param>
/// <param name="Score">Higher is a better match (the negated bm25 rank, title weighted over path over text).</param>
/// <param name="Matched">'title', 'path' or 'text': the first of these that holds a query word.</param>
/// <param name="Snippet">The matched field around the first query word, cleaned like every answer's text and fenced when untrusted.</param>
/// <param name="Trust">Whether the node's label and snippet may be read as instructions.</param>
public sealed record MapSearchHit(NodeKey Key, NodeKind Kind, string Label, string? Repo, string Area, double Score, string Matched, string Snippet, Results.Trust Trust);
