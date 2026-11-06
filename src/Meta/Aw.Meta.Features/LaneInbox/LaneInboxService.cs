using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.LaneInbox;

/// <summary>
/// What a lane's hook shows the lane: the messages the meta sent that it hasn't seen. They are marked delivered (they stay owed a reply
/// until the lane replies), and a call made by a hook (AW_META_HOOK set) is the lane's heartbeat; one typed by hand only notes that the CLI was used. With no team set up it answers empty and records nothing.
/// </summary>
/// <param name="config">The team config.</param>
/// <param name="mail">The lanes' inboxes.</param>
/// <param name="beats">The lane hooks' heartbeats.</param>
/// <param name="clock">Dates the delivery.</param>
internal sealed class LaneInboxService(IMetaConfig config, ILaneMail mail, LaneBeats beats, TimeProvider clock) : IService<LaneInboxRequest, LaneInboxResponse>
{
    public async ValueTask<Result<LaneInboxResponse>> Handle(LaneInboxRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        if (team.IsError)
        {
            return team.Error.Code == MetaErrors.NoTeamConfig.Code ? new LaneInboxResponse([]) : team.Error;
        }

        var now = clock.GetUtcNow();
        var beat = request.HookEvent is { Length: > 0 }
            ? await beats.Record(request.Identity.Lane, stopped: false, now, cancellationToken).ConfigureAwait(false)
            : await beats.RecordCli(request.Identity.Lane, now, cancellationToken).ConfigureAwait(false);
        if (beat.IsError)
        {
            return beat.Error;
        }

        var owed = await mail.Owed(request.Identity.Lane, cancellationToken).ConfigureAwait(false);
        if (owed.IsError)
        {
            return owed.Error;
        }

        var unread = owed.Value.Where(m => !m.Delivered).ToList();
        var delivered = unread.Count == 0 ? Results.Ok() : await mail.Deliver([.. unread.Select(m => m.Seq)], now, cancellationToken).ConfigureAwait(false);
        return delivered.IsError ? delivered.Error : new LaneInboxResponse(unread);
    }
}
