namespace Aw.Context.Core.Concepts;

/// <summary>One group of features waiting for a name: what the namer is told about it (titles, never paths).</summary>
/// <param name="Seed">The key of the feature the group grew from.</param>
/// <param name="Label">The seed feature's label.</param>
/// <param name="Features">The titles of the member features.</param>
/// <param name="Records">The titles of the records that mention them.</param>
public sealed record ConceptGroup(string Seed, string Label, IReadOnlyList<string> Features, IReadOnlyList<string> Records);
