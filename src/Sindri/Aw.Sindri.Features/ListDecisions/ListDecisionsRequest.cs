using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.ListDecisions;

/// <summary>List decisions (GET /sindri/decisions?pending=&amp;item=; API spec §3.1; 'sindri decisions').</summary>
/// <param name="Pending">True for the pending ones, false for the decided and voided ones, null for all.</param>
/// <param name="Item">Only decisions about this item (id or alias), or null.</param>
public sealed record ListDecisionsRequest(bool? Pending, ItemRef? Item) : IQuery<ListDecisionsResponse>;
