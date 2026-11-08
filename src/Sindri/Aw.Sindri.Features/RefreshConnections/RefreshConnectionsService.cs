using Aw.Kernel;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.OAuth;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.RefreshConnections;

/// <summary>
/// The 'connection-refresh' background service (connections PR 10b), every 5 minutes: each 'connected' oauth connection whose access token expires within 10 minutes
/// of now (the clock port, never the wall clock) is refreshed through <see cref="IOAuthClient"/> and its secret replaced, keeping the old refresh token when the provider returns none.
/// A refresh that fails sets the connection to 'needs-sign-in' with the provider's error code as the reason (never a token) and appends ONE 'connection.needs-sign-in'
/// event, which projects the Needs-you row 'connection:&lt;name&gt;'. Other kinds, other statuses and later expiries are left alone, so a failed connection is not retried (and not announced) again.
/// </summary>
/// <param name="store">The connections.</param>
/// <param name="secrets">Reads and replaces the tokens.</param>
/// <param name="presets">The shipped presets (the token endpoint).</param>
/// <param name="oauth">Refreshes a token.</param>
/// <param name="ledger">Appends the event.</param>
/// <param name="events">Builds the entry.</param>
/// <param name="clock">The time.</param>
internal sealed class RefreshConnectionsService(IConnectionStore store, IConnectionSecrets secrets, IConnectionPresetCatalog presets, IOAuthClient oauth, ILedgerStore ledger, EventFactory events, TimeProvider clock)
    : IScheduledService
{
    internal static ServiceName ServiceId { get; } = ServiceName.From("connection-refresh");

    /// <summary>What the service does, as GET /sindri/services says it.</summary>
    public const string Description = "Refreshes the tokens of connections that expire within 10 minutes.";

    /// <summary>Runs without a profile entry, every 5 minutes.</summary>
    internal static BuiltInService BuiltIn { get; } = new(ServiceId, new Cadence(TimeSpan.FromMinutes(5)), Description);

    public ServiceName Identity() => ServiceId;

    public ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken)
    {
        _ = (store, secrets, presets, oauth, ledger, events, clock, run, cancellationToken);
        throw new NotSupportedException();
    }
}
