namespace Aw.Sindri.Ui.Api.Context;

/// <summary>One concept's terms.</summary>
/// <param name="Id">The concept's id, a map key.</param>
/// <param name="Name">The concept's name.</param>
/// <param name="Terms">Its terms, most telling first.</param>
internal sealed record ConceptTerms(string Id, string Name, IReadOnlyList<string> Terms);
