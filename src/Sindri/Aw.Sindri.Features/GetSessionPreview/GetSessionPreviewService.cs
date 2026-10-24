using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.GetSessionPreview;

/// <summary>
/// The live read-only preview of a session's tmux pane (collector spec CA-9, CI-10). The checks run in this order, per request and before any shared wait: the caller
/// kind (a caller is needed), the ref, expectStartedAt, then the answers that are still HTTP 200. Nothing is written to the ledger and no
/// pane text is logged.
/// </summary>
internal sealed class GetSessionPreviewService(ISessionSnapshot snapshot, ICallerContextAccessor callers, PaneCaptures captures, IProcessTable processes, TimeProvider time)
    : IService<GetSessionPreviewRequest, GetSessionPreviewResponse>
{
    public async ValueTask<Result<GetSessionPreviewResponse>> Handle(GetSessionPreviewRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var located = Locate(request);
        if (!located.IsSuccess)
        {
            return located.Error;
        }

        var found = located.Value;
        if (found.Host.Tmux is not { Socket: { } socket, Pane: { } pane } || found.Host.Kind != "tmux")
        {
            return Unavailable(PreviewReason.NotInTmux);
        }

        var read = await captures.Share(socket, pane).WaitAsync(cancellationToken).ConfigureAwait(false);
        return read.Kind switch
        {
            PaneReadKind.TimedOut => Unavailable(PreviewReason.TimedOut),
            PaneReadKind.Captured when InPane(found, read.PanePid, cancellationToken) => Show(found, read),
            PaneReadKind.Captured or PaneReadKind.Gone => Unavailable(PreviewReason.NotLive),
            _ => Unavailable(PreviewReason.NotLive),
        };
    }

    /// <summary>The refusals, in order: the caller kind, the collector's state, the ref, then expectStartedAt.</summary>
    private Result<ObservedSession> Locate(GetSessionPreviewRequest request)
    {
        if (callers.Current is null)
        {
            return SindriErrors.AuthRefused.With("a session needs a caller");
        }

        var now = snapshot.Current;   // one read: the check and the row come from the same pass
        if (now is not null && SessionRows.Unavailable(now.Problems) is { } unavailable)
        {
            return unavailable;
        }

        if ((now is null ? null : SessionRows.Find(now, request.Ref)) is not { } found)
        {
            return SindriErrors.SessionNotFound.With("no live session has that ref");
        }

        return request.ExpectStartedAt is { } expected && expected != found.StartedAt
            ? SindriErrors.SessionConflict.With("the session at that ref started at another time; read it again")
            : found;
    }

    private bool InPane(ObservedSession session, int panePid, CancellationToken cancellationToken)
    {
        var table = processes.Read(cancellationToken);
        return table.IsSuccess && PreviewRules.InPane(session.Pid, session.StartTicks, panePid, table.Value);
    }

    private SessionPreview Show(ObservedSession session, PaneRead read)
    {
        var text = PreviewRules.Of(read.Capture.Span);
        return new SessionPreview(text.Lines, text.Truncated, time.GetUtcNow(), session.StartedAt, new PreviewHost(session.Host.Kind, session.Host.Label));
    }

    private SessionPreviewUnavailable Unavailable(string reason) => new(reason, time.GetUtcNow());
}
