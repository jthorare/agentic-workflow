using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Requests;

namespace Aw.Sindri.Features.ListRequests;

/// <summary>A request as the list shows it (U3-API-4).</summary>
/// <param name="Id">The request.</param>
/// <param name="Kind">What it asks.</param>
/// <param name="Item">The item it is about, or null.</param>
/// <param name="Repo">Its repo, or null.</param>
/// <param name="Text">What the caller wrote, scrubbed.</param>
/// <param name="Actor">Who filed it.</param>
/// <param name="State">Where it stands.</param>
/// <param name="DecisionId">The decision that settles it.</param>
/// <param name="FiledAt">When it was filed.</param>
public sealed record RequestView(RequestId Id, RequestKind Kind, ItemId? Item, RepoName? Repo, string Text, string Actor, RequestState State, DecisionId DecisionId, DateTimeOffset FiledAt);
