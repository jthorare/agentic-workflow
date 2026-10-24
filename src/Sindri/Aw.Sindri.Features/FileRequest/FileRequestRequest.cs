using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Requests;

namespace Aw.Sindri.Features.FileRequest;

/// <summary>File a request (POST /sindri/requests, 'sindri request'; API spec §3.2): request class, appends 'request.filed' and creates one pending decision.</summary>
/// <param name="Kind">What it asks: rescope, add-task, note or handback.</param>
/// <param name="Text">What to say: 1 to 65,536 characters.</param>
/// <param name="Item">The item it is about, by id or alias; null (or a body without the key) for a request about no item (the caller's X-Sindri-Repo scopes it).</param>
public sealed record FileRequestRequest(RequestKind Kind, string Text, ItemRef? Item = null) : ICommand<RequestFiledResponse>;
