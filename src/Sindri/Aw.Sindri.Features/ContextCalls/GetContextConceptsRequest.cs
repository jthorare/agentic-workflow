using Aw.Context.Core.Concepts;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>One page of the product's concepts (GET /sindri/context/concepts).</summary>
/// <param name="Repo">A human caller's repo when the profile has more than one, or null.</param>
/// <param name="Cursor">The previous answer's 'next', or null for the first page.</param>
/// <param name="Limit">Concepts per page (100 when null; there is no upper limit).</param>
/// <param name="Q">Keeps the concepts whose name, summary or a member's label contains this text, or null for all.</param>
/// <param name="Draft">True to read the draft map.</param>
/// <param name="MapVersion">A retained map version ('v3', '3' or 'v3@a41b07d'), or null for the current one; not valid with <paramref name="Draft"/>.</param>
public sealed record GetContextConceptsRequest(string? Repo = null, string? Cursor = null, int? Limit = null, string? Q = null, bool Draft = false, string? MapVersion = null) : IQuery<ConceptsView>;
