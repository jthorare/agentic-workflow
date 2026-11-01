namespace Aw.Context.Core.Concepts;

/// <summary>One term of a concept and how many records its alias would newly link.</summary>
/// <param name="Word">The term, cleaned like every label.</param>
/// <param name="NewRecords">The visible live records whose text holds the term by the alias rule and that the concept does not link yet.</param>
public sealed record PinSuggestTerm(string Word, int NewRecords);
