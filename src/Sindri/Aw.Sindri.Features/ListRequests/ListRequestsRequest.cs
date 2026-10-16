using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Requests;

namespace Aw.Sindri.Features.ListRequests;

/// <summary>List requests (GET /sindri/requests?state=&amp;item=; API spec §3.1; U3-API-4).</summary>
/// <param name="State">Only requests in this state, or null.</param>
/// <param name="Item">Only requests about this item (id or alias), or null.</param>
public sealed record ListRequestsRequest(RequestState? State, ItemRef? Item) : IQuery<ListRequestsResponse>;
