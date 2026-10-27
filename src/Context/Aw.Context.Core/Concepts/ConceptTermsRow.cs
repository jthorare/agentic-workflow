using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Concepts;

/// <summary>One concept and the words that characterize it.</summary>
/// <param name="Id">The concept's key.</param>
/// <param name="Name">Its name as the caller reads it.</param>
/// <param name="Terms">Its terms, the most telling first.</param>
public sealed record ConceptTermsRow(NodeKey Id, string Name, IReadOnlyList<string> Terms);
