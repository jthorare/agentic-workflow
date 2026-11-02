using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Glossary;

/// <summary>A word that characterizes some clusters, and the features of every cluster it characterizes.</summary>
/// <param name="Term">The lower-cased word.</param>
/// <param name="Weight">The highest TF-IDF score the word had in any cluster.</param>
/// <param name="Features">The features of those clusters, in key order.</param>
public sealed record GlossaryTerm(string Term, double Weight, IReadOnlyList<NodeKey> Features);
