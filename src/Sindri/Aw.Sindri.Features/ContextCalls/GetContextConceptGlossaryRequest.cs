using Aw.Context.Core.Concepts;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The glossary of the product's concepts (GET /sindri/context/concepts/glossary): the words that characterize each concept, and for each word the concepts it characterizes.</summary>
/// <param name="Repo">A human caller's repo when the profile has more than one, or null.</param>
/// <param name="Draft">True to read the draft map.</param>
/// <param name="MapVersion">A retained map version ('v3', '3' or 'v3@a41b07d'), or null for the current one; not valid with <paramref name="Draft"/>.</param>
public sealed record GetContextConceptGlossaryRequest(string? Repo = null, bool Draft = false, string? MapVersion = null) : IQuery<ConceptGlossaryView>;
