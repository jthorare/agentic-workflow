using Aw.Context.Core.Concepts;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>One page of one concept's members (GET /sindri/context/concepts/{id}/members).</summary>
/// <param name="Concept">The concept's key ('concept:scheduling'), or 'concept:other'.</param>
/// <param name="Cursor">The previous answer's 'next', or null for the first page.</param>
/// <param name="Limit">Members per page (100 when null; there is no upper limit).</param>
/// <param name="Repo">A human caller's repo when the profile has more than one, or null.</param>
/// <param name="Draft">True to read the draft map.</param>
/// <param name="MapVersion">A retained map version, or null for the current one; not valid with <paramref name="Draft"/>.</param>
public sealed record GetContextConceptMembersRequest(string Concept, string? Cursor = null, int? Limit = null, string? Repo = null, bool Draft = false, string? MapVersion = null) : IQuery<ConceptMembersView>;
