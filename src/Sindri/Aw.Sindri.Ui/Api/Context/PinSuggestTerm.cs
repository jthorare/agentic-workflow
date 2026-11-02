namespace Aw.Sindri.Ui.Api.Context;

/// <summary>One term of a concept and how many records its alias would newly link.</summary>
/// <param name="Word">The term.</param>
/// <param name="NewRecords">The visible records whose text holds it and that the concept does not link yet.</param>
internal sealed record PinSuggestTerm(string Word, int NewRecords);
