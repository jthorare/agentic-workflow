using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Requests;

namespace Aw.Sindri.Core.Views;

/// <summary>A row of the 'requests' view: one filed request, its text already scrubbed and escaped by the projection.</summary>
/// <param name="Id">The request.</param>
/// <param name="Kind">What it asks.</param>
/// <param name="Item">The item it is about, or null.</param>
/// <param name="Repo">Its repo (the item's, else the caller's header), or null.</param>
/// <param name="Text">What the caller wrote.</param>
/// <param name="Actor">The actor that filed it, verbatim from the envelope.</param>
/// <param name="State">Where it stands.</param>
/// <param name="Decision">The decision that settles it.</param>
/// <param name="FiledAt">When it was filed.</param>
/// <param name="Sequence">The ledger sequence that last changed the row.</param>
public sealed record RequestRow(
    RequestId Id,
    RequestKind Kind,
    ItemId? Item,
    RepoName? Repo,
    string Text,
    string Actor,
    RequestState State,
    DecisionId Decision,
    DateTimeOffset FiledAt,
    long Sequence);
