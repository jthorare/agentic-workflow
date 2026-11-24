namespace Aw.Context.Core.Concepts;

/// <summary>What <see cref="ConceptMerge"/> compares of one concept.</summary>
/// <param name="Id">The concept's key.</param>
/// <param name="Terms">Its glossary terms.</param>
/// <param name="CodeFiles">The code files behind its features.</param>
/// <param name="Members">How many features, records and decisions it holds: the biggest of a group leads.</param>
/// <param name="Pinned">True for a pin, which never merges.</param>
internal sealed record ConceptTraits(string Id, IReadOnlyList<string> Terms, IReadOnlyList<string> CodeFiles, int Members, bool Pinned);
