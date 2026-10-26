namespace Aw.Context.Core.Concepts;

/// <summary>'GET /sindri/context/concepts/glossary': the words the map's concepts are named from, by concept and by word.</summary>
/// <param name="ApiVersion">1.</param>
/// <param name="Repo">The repo this answer is for, or null.</param>
/// <param name="MapVersion">The map read ('v13').</param>
/// <param name="State">'ok', or 'empty' when no concept carries a term (no embeddings, or the map was built without them).</param>
/// <param name="Concepts">The concepts that carry terms, in key order.</param>
/// <param name="Terms">Every term with the concepts it characterizes, by term.</param>
public sealed record ConceptGlossaryView(
    int ApiVersion,
    string? Repo,
    string MapVersion,
    string State,
    IReadOnlyList<ConceptTermsRow> Concepts,
    IReadOnlyList<TermConceptsRow> Terms);
