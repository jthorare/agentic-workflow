using Aw.Kernel;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Core.Coordination;

/// <summary>Counts the meta's inbound messages: every one stored, and those still owed a reply (what "aw-meta inbox" lists).</summary>
/// <param name="config">The team config, for the meta lane.</param>
/// <param name="messages">The messages.</param>
/// <param name="reader">The open obligations.</param>
/// <param name="clock">Decides whether a deferral is still running.</param>
public sealed class InboxCounts(IMetaConfig config, IMessageStore messages, ObligationReader reader, TimeProvider clock)
{
    /// <summary>Counts the inbox.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The total stored and the number still owed a reply.</returns>
    public async ValueTask<Result<(int Total, int Open)>> Read(CancellationToken cancellationToken)
    {
        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        var inbound = await messages.Messages(cancellationToken).ConfigureAwait(false);
        if (team.IsError || inbound.IsError)
        {
            return team.IsError ? team.Error : inbound.Error;
        }

        var open = await reader.Open(team.Value.MetaLane, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return open.IsError ? open.Error : (inbound.Value.Count, open.Value.Count(static o => o.Kind == ObligationKind.Reply));
    }
}
