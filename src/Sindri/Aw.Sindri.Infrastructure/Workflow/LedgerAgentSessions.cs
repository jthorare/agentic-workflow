using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>
/// An agent Step's session from the ledger (engine P8 D3, D9): 'session.ended:&lt;launch&gt;' says it ended, and the latest
/// 'session.usage:&lt;launch&gt;:&lt;n&gt;' sample says what it spent (samples are cumulative). The item the launch belongs to comes from the 'launches' view,
/// so each read scans only that item's session events. A launch the view doesn't know is live with no spend.
/// </summary>
/// <param name="views">The launches view, for the launch's item.</param>
/// <param name="ledger">The ledger, read-only.</param>
/// <param name="events">Reads a payload.</param>
internal sealed class LedgerAgentSessions(ILaunchViews views, ILedgerReader ledger, EventFactory events) : IAgentSessions
{
    private const int Page = 500;

    public async ValueTask<Result<AgentSessionState>> Watch(LaunchId launch, CancellationToken cancellationToken)
    {
        var sessions = await Session(launch, cancellationToken).ConfigureAwait(false);
        if (sessions.IsError)
        {
            return sessions.Error;
        }

        var ended = sessions.Value.FirstOrDefault(static e => e.Envelope.Type == EventTypes.SessionEnded);
        if (ended is null)
        {
            return AgentSessionState.Live;
        }

        var payload = events.Payload<SessionEnded>(ended);
        return payload.IsError ? payload.Error : Ended(payload.Value);
    }

    private static AgentSessionState Ended(SessionEnded ended) => ended switch
    {
        { Reason: SessionEndReason.Exit } => new AgentSessionState(AgentSessionEnd.Exit, ended.ExitCode),
        { Reason: SessionEndReason.Limit, Kind: { } kind, Limit: { } limit, Peak: { } peak } => new AgentSessionState(AgentSessionEnd.Limit, ExitCode: null, new AgentSessionLimit(kind, limit, peak)),
        _ => new AgentSessionState(AgentSessionEnd.Lost, ended.ExitCode),
    };

    public async ValueTask<Result<long>> Usage(LaunchId launch, CancellationToken cancellationToken)
    {
        var sessions = await Session(launch, cancellationToken).ConfigureAwait(false);
        if (sessions.IsError)
        {
            return sessions.Error;
        }

        var latest = 0L;
        var sample = 0;
        foreach (var recorded in sessions.Value.Where(static e => e.Envelope.Type == EventTypes.SessionUsage))
        {
            var payload = events.Payload<SessionUsage>(recorded);
            if (payload.IsError)
            {
                return payload.Error;
            }

            if (payload.Value.Sample > sample)
            {
                (sample, latest) = (payload.Value.Sample, payload.Value.InputTokens + payload.Value.OutputTokens);
            }
        }

        return latest;
    }

    /// <summary>The launch's 'session.ended' and 'session.usage' events, oldest first: the item's session events whose key names the launch.</summary>
    private async ValueTask<Result<IReadOnlyList<LedgerEvent>>> Session(LaunchId launch, CancellationToken cancellationToken)
    {
        var row = await views.Get(launch, cancellationToken).ConfigureAwait(false);
        if (row.IsError)
        {
            return row.Error;
        }

        if (row.Value is not { } known)
        {
            return Array.Empty<LedgerEvent>();
        }

        var filter = new LedgerFilter(TypeFilter.Parse($"{EventTypes.SessionEnded},{EventTypes.SessionUsage}").Value, known.Item);
        var mine = new List<LedgerEvent>();
        var cursor = 0L;
        var head = await ledger.Head(cancellationToken).ConfigureAwait(false);
        while (head.IsSuccess && cursor < head.Value)
        {
            var scan = await ledger.Scan(cursor, Page, filter, cancellationToken).ConfigureAwait(false);
            if (scan.IsError)
            {
                return scan.Error;
            }

            mine.AddRange(scan.Value.Events.Where(e => IsOf(e.IdempotencyKey, launch)));
            if (scan.Value.ScannedTo <= cursor)
            {
                break;
            }

            cursor = scan.Value.ScannedTo;
        }

        return head.IsError ? head.Error : mine;
    }

    private static bool IsOf(string key, LaunchId launch) =>
        string.Equals(key, $"{EventTypes.SessionEnded}:{launch.Value}", StringComparison.Ordinal)
        || key.StartsWith($"{EventTypes.SessionUsage}:{launch.Value}:", StringComparison.Ordinal);
}
