using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Requests;

namespace Aw.Sindri.Core.Events;

/// <summary>The public form of 'request.filed' (API spec §3.4): the payload without its text.</summary>
/// <param name="CommandId">The 'FileRequest' command's id.</param>
/// <param name="Request">The new request.</param>
/// <param name="Decision">The decision that settles it.</param>
/// <param name="Kind">What it asks.</param>
/// <param name="Item">The item it is about, or null.</param>
/// <param name="Repo">Its repo, or null.</param>
public sealed record RequestFiledPublic(CommandId CommandId, RequestId Request, DecisionId Decision, RequestKind Kind, ItemId? Item, RepoName? Repo);
