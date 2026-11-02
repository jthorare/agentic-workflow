namespace Aw.Sindri.Core.Setup;

/// <summary>One concept read from a pasted list: the name, its definition and the words people use for it.</summary>
/// <param name="Name">The concept's name.</param>
/// <param name="Summary">The definition, or null when the line gave none.</param>
/// <param name="Aliases">The words of a trailing '(also: word, word)', or empty.</param>
public sealed record ConceptLine(string Name, string? Summary, IReadOnlyList<string> Aliases);
