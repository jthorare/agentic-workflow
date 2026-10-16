using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Requests;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'request.filed' payload (API spec §3.4): a session or a human asked for something. Its public form, <see cref="RequestFiledPublic"/>, leaves the text out.</summary>
/// <param name="CommandId">The 'FileRequest' command's id.</param>
/// <param name="Request">The new request.</param>
/// <param name="Decision">The decision that settles it, created with it.</param>
/// <param name="Kind">What it asks.</param>
/// <param name="Item">The item it is about, or null.</param>
/// <param name="Repo">Its repo, or null.</param>
/// <param name="Text">What the caller wrote (the projection scrubs and escapes it).</param>
public sealed record RequestFiled(CommandId CommandId, RequestId Request, DecisionId Decision, RequestKind Kind, ItemId? Item, RepoName? Repo, string Text);
