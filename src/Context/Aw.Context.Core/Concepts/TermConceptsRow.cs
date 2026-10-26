using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Concepts;

/// <summary>One word and the concepts it characterizes.</summary>
/// <param name="Term">The lower-cased word.</param>
/// <param name="Concepts">The keys of the concepts that carry it, in key order.</param>
public sealed record TermConceptsRow(string Term, IReadOnlyList<NodeKey> Concepts);
