namespace Aw.Sindri.Ui.Api.Context;

/// <summary>GET /sindri/context/concepts/glossary: the words the concepts are named from, by concept and by word. The concept card's top terms come from here.</summary>
/// <param name="ApiVersion">The answer's version.</param>
/// <param name="Repo">The repo the glossary is of; null when the profile has several and none was asked for.</param>
/// <param name="MapVersion">The map version the glossary was read at.</param>
/// <param name="State">ok, or empty (no terms yet: not an error).</param>
/// <param name="Concepts">The concepts that carry terms, in key order.</param>
/// <param name="Terms">Every term with the concepts it characterizes.</param>
internal sealed record ConceptGlossaryView(
    int ApiVersion,
    string? Repo,
    string MapVersion,
    string State,
    IReadOnlyList<ConceptTerms> Concepts,
    IReadOnlyList<TermConcepts> Terms);
