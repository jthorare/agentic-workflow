using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.LaneSend;

/// <summary>
/// The meta's send to a non-Claude lane, which has no SendMessage: the message goes into the lane's inbox (its hooks deliver it at
/// the next prompt or stop) and is recorded as a send to "team-lane", which closes the meta's reply obligation the way a
/// SendMessage does. An idle lane whose agent is up is woken with one typed line, which counts only once the lane's next hook answers it.
/// </summary>
/// <param name="config">The team config.</param>
/// <param name="files">The lane records.</param>
/// <param name="mail">The lanes' inboxes.</param>
/// <param name="messages">Records the send.</param>
/// <param name="beats">The lane hooks' heartbeats.</param>
/// <param name="pane">The lane's tmux session.</param>
/// <param name="clock">Dates the send.</param>
internal sealed class LaneSendService(IMetaConfig config, ILaneFiles files, ILaneMail mail, IMessageStore messages, LaneBeats beats, ILanePane pane, TimeProvider clock)
    : IService<LaneSendRequest, LaneSendResponse>
{
    internal const string WakeLine = LaneDelivery.WakeLine;

    public async ValueTask<Result<LaneSendResponse>> Handle(LaneSendRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        if (team.IsError)
        {
            return team.Error;
        }

        var name = team.Value.Lanes.FirstOrDefault(l => string.Equals(l, request.Lane, StringComparison.OrdinalIgnoreCase) && !string.Equals(l, team.Value.MetaLane, StringComparison.OrdinalIgnoreCase));
        if (name is null)
        {
            return MetaErrors.UnknownLane.With($"'{request.Lane}' is not a lane of team {team.Value.Team} (lanes: {string.Join(", ", team.Value.Lanes.Where(l => !string.Equals(l, team.Value.MetaLane, StringComparison.OrdinalIgnoreCase)))})");
        }

        var record = await files.Record(name, cancellationToken).ConfigureAwait(false);
        if (record.IsError)
        {
            return record.Error;
        }

        return record.Value is { IsNonClaude: true, Agent: { } agent } found
            ? await Deliver(new LaneTarget(team.Value.Team, name, found, agent), request, cancellationToken).ConfigureAwait(false)
            : MetaErrors.ClaudeLane.With($"lane {name} runs Claude Code (or the launcher recorded no other agent for it): use SendMessage to {team.Value.Team}-{name}");
    }

    private async ValueTask<Result<LaneSendResponse>> Deliver(LaneTarget target, LaneSendRequest request, CancellationToken cancellationToken)
    {
        var delivered = await new LaneDelivery(files, mail, messages, beats, pane, clock).Deliver(target, request.Text, TimeSpan.FromSeconds(request.WakeConfirmSeconds), cancellationToken).ConfigureAwait(false);
        return delivered.IsError
            ? delivered.Error
            : new LaneSendResponse(target.Lane, delivered.Value.Id, delivered.Value.Woken, delivered.Value.WakeSkipped, delivered.Value.Attention) { ViewerAttached = delivered.Value.ViewerAttached };
    }
}
