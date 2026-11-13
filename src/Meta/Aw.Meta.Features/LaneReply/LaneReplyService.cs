using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Logs;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.LaneReply;

/// <summary>
/// A non-Claude lane answers the meta. The reply becomes an inbound message of the meta (id "lane:&lt;lane&gt;:&lt;n&gt;", from "team-lane"),
/// so the meta's stop-check derives a reply obligation from it and the meta's next lane-send to that lane closes it; what the lane owed
/// is cleared. The running log records team, lane, provider and session. A blank reply, or no team set up, records nothing.
/// </summary>
/// <param name="config">The team config.</param>
/// <param name="files">The lane records.</param>
/// <param name="mail">The lanes' inboxes.</param>
/// <param name="log">The running log.</param>
/// <param name="clock">Dates the reply.</param>
/// <param name="obligations">Writes obligation.opened for the reply; null to write none.</param>
internal sealed class LaneReplyService(IMetaConfig config, ILaneFiles files, ILaneMail mail, IRunningLog log, TimeProvider clock, ObligationEventWriter? obligations = null) : IService<LaneReplyRequest, LaneReplyResponse>
{
    public async ValueTask<Result<LaneReplyResponse>> Handle(LaneReplyRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        if (team.IsError)
        {
            return team.Error.Code == MetaErrors.NoTeamConfig.Code ? new LaneReplyResponse(Recorded: false, Id: null) : team.Error;
        }

        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return new LaneReplyResponse(Recorded: false, Id: null);
        }

        var record = await files.Record(request.Identity.Lane, cancellationToken).ConfigureAwait(false);
        if (record.IsError || record.Value is not { Session: { } session } found)
        {
            return record.IsError ? record.Error : MetaErrors.LaneNotRecorded.With($"no lane-sessions/{request.Identity.Lane} record: the lane's session-id hook has not reported yet");
        }

        var now = clock.GetUtcNow();
        var stored = await mail.Reply(request.Identity, request.Text.Trim(), now, cancellationToken).ConfigureAwait(false);
        if (stored.IsError)
        {
            return stored.Error;
        }

        await OpenObligation(request, stored.Value.Value, now, cancellationToken).ConfigureAwait(false);
        _ = await log.Append(LogLine.Event(LogKind.Note, now, $"lane reply {stored.Value.Value}: team {request.Identity.Team}, lane {request.Identity.Lane}, provider {found.Agent?.Agent ?? "unknown"}, session {session.Value}"), cancellationToken).ConfigureAwait(false);
        return new LaneReplyResponse(Recorded: true, stored.Value);
    }

    // A reply the meta owes an answer to opens an obligation; a notice opens none.
    private async ValueTask OpenObligation(LaneReplyRequest request, string id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (obligations is not null && !NoticeRule.IsNotice(request.Text))
        {
            await obligations.Opened(now, id, request.Identity.SessionName, cancellationToken).ConfigureAwait(false);
        }
    }
}
