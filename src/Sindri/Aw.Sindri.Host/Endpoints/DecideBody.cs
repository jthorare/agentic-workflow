using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.Decide;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of POST /sindri/decisions/{decisionId}: '{choice, note?}'; the decision comes from the route.</summary>
/// <param name="Choice">One of the decision's options.</param>
/// <param name="Note">A note, or null.</param>
internal sealed record DecideBody(DecisionOption Choice, string? Note = null)
{
    /// <summary>The request for <paramref name="decision"/>.</summary>
    /// <param name="decision">The route's decision.</param>
    /// <returns>The request.</returns>
    internal DecideRequest ToRequest(DecisionId decision) => new(decision, Choice, Note);
}
