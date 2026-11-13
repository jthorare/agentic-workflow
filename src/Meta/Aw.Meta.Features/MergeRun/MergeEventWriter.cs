using Aw.Meta.Core.Config;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Events;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.MergeRun;

/// <summary>Writes merge.recorded: the pull request, the merge commit and the lane whose ready notice asked for it.</summary>
/// <param name="events">Appends the event.</param>
/// <param name="messages">Holds the ready notices.</param>
/// <param name="clock">Dates the event.</param>
internal sealed class MergeEventWriter(MetaEventSink events, IMessageStore messages, TimeProvider clock)
{
    internal async ValueTask Write(MergeRunResponse merged, TeamConfig team, CancellationToken cancellationToken)
    {
        var lane = await Lane(merged.Pr, team, cancellationToken).ConfigureAwait(false);
        await events.Emit(MetaEvents.MergeRecorded(clock.GetUtcNow(), merged.Pr, merged.Sha ?? Core.Obligations.MergeRecord.Unknown, lane), cancellationToken).ConfigureAwait(false);
    }

    // The newest ready notice for the pull request names the lane ("t1-api" is lane "api"); empty when none is stored.
    private async ValueTask<string> Lane(int pr, TeamConfig team, CancellationToken cancellationToken)
    {
        var all = await messages.Messages(cancellationToken).ConfigureAwait(false);
        if (all.IsError)
        {
            return string.Empty;
        }

        var sender = all.Value
            .Where(m => m.Class is { Kind: MessageKind.Ready } ready && ready.Pr == pr)
            .OrderByDescending(static m => m.At)
            .Select(static m => m.FromName)
            .FirstOrDefault(static name => name.Length > 0);
        return sender is null ? string.Empty : LaneNames.WithoutTeam(sender, team.Team);
    }
}
