using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Features.FileRequest;

/// <summary>
/// Files a request (API spec §3.2, P4 plan Unit D): inside the gate, after the views caught up, it settles the item and repo
/// (<see cref="RequestTarget"/>: the session's repo scope, a closed item, the caps), scrubs the text, and appends one 'request.filed'
/// keyed by the new request id. The request, its decision and the needs-you row are projections of that one event (D1).
/// </summary>
/// <param name="target">Settles the item, the repo and the caps.</param>
/// <param name="ledger">Appends 'request.filed'.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="events">Builds the entry.</param>
/// <param name="ids">The id source for the request and its decision.</param>
/// <param name="scrubber">Takes secrets out of the text before it reaches the ledger.</param>
/// <param name="callers">The caller: the command id, the class and the header repo.</param>
internal sealed class FileRequestService(RequestTarget target, ILedgerStore ledger, HeldLease held, EventFactory events, IIdGenerator ids, ITextScrubber scrubber, ICallerContextAccessor callers)
    : IService<FileRequestRequest, RequestFiledResponse>
{
    public async ValueTask<Result<RequestFiledResponse>> Handle(FileRequestRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("FileRequest needs a caller");
        }

        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var resolved = await target.Resolve(request, caller, cancellationToken).ConfigureAwait(false);
        if (resolved.IsError)
        {
            return resolved.Error;
        }

        var (item, repo) = resolved.Value;
        var (requestId, decision) = (RequestId.New(ids), DecisionId.New(ids));
        var text = SafeText.CleanBlock(scrubber, request.Text, RequestRules.TextMax);
        var filed = new RequestFiled(caller.CommandId, requestId, decision, request.Kind, item, repo, text);
        var entry = events.Entry(EventTypes.RequestFiled, filed, $"{EventTypes.RequestFiled}:{requestId.Value}", item?.Value);
        var appended = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : new RequestFiledResponse(new CommandAccepted(caller.CommandId, appended.Value.Sequence), requestId, decision);
    }
}
