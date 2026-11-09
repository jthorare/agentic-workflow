using Aw.Kernel;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Lease;

namespace Aw.Sindri.Features.PollConnections;

/// <summary>What one provider call did.</summary>
/// <param name="Cursor">The cursor as it stands after the call.</param>
/// <param name="Keys">The delivery ids the provider returned.</param>
/// <param name="Started">Runs started or queued.</param>
/// <param name="Skipped">Triggers skipped.</param>
/// <param name="CursorMoved">True when the saved cursor value changed.</param>
/// <param name="RateLimited">True when the provider asked to wait.</param>
/// <param name="Gap">True when the poller skipped older events.</param>
/// <param name="Reason">The poller's reason, or null.</param>
internal sealed record RoundOutcome(PollCursor Cursor, IReadOnlyList<string> Keys, int Started, int Skipped, bool CursorMoved, bool RateLimited, bool Gap, string? Reason);

/// <summary>One provider call for one (connection, event kind): fan out to the triggers, start the runs, then save the cursor.</summary>
internal sealed class PollRound
{
    internal PollRound(IGraphRunStarter starter, IPollCursors cursors, ConnectionEvents events, TimeProvider clock) => _ = (starter, cursors, events, clock);

    internal ValueTask<Result<RoundOutcome>> Run(PollGroup group, Connection connection, IEventPoller poller, PollCursor? saved, LeaseGrant grant, CancellationToken cancellationToken) =>
        throw new NotSupportedException("unbuilt: " + group + connection + poller + saved + grant + cancellationToken);
}
