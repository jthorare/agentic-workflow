using Aw.Kernel;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Core.Coordination;

/// <summary>
/// Counts the meta's inbound messages: every one stored but the status lines, those still owed a reply (what "aw-meta inbox" lists), and the
/// lanes that sent each status line (#2651: a STATUS:-tagged message is batched, never counted as new mail).
/// </summary>
/// <param name="config">The team config, for the meta lane.</param>
/// <param name="messages">The messages.</param>
/// <param name="reader">The open obligations.</param>
/// <param name="clock">Decides whether a deferral is still running.</param>
public sealed class InboxCounts(IMetaConfig config, IMessageStore messages, ObligationReader reader, TimeProvider clock)
{
    /// <summary>Counts the inbox.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The total stored but the status lines, the number still owed a reply, and the sending lane of each status line in arrival order.</returns>
    public async ValueTask<Result<(int Total, int Open, IReadOnlyList<string> Status)>> Read(CancellationToken cancellationToken)
    {
        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        var inbound = await messages.Messages(cancellationToken).ConfigureAwait(false);
        if (team.IsError || inbound.IsError)
        {
            return team.IsError ? team.Error : inbound.Error;
        }

        var obligations = await reader.Open(team.Value.MetaLane, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        if (obligations.IsError)
        {
            return obligations.Error;
        }

        var now = clock.GetUtcNow();
        var owed = obligations.Value.Where(static o => o.Kind == ObligationKind.Reply).Select(static o => o.Key).ToHashSet(StringComparer.Ordinal);
        var status = inbound.Value.Where(static m => NoticeRule.IsStatusTagged(m.Body)).Select(m => Lane(m, team.Value.Team)).ToList();
        var open = inbound.Value.Count(m => string.Equals(InboxState.Of(m, owed.Contains(m.Id.Value), now), InboxState.Open, StringComparison.Ordinal));
        return (inbound.Value.Count - status.Count, open, status);
    }

    // The lane a message came from: its session name without the "team-" prefix, or the socket address when it had no name.
    private static string Lane(InboxMessage message, string team)
    {
        var name = message.FromName.Length > 0 ? message.FromName : message.From;
        var prefix = team + "-";
        return name.StartsWith(prefix, StringComparison.Ordinal) && name.Length > prefix.Length ? name[prefix.Length..] : name;
    }
}
