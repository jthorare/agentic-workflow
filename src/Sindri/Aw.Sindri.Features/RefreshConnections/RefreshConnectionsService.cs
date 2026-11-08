using System.Globalization;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.OAuth;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
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

    private static readonly TimeSpan _horizon = TimeSpan.FromMinutes(10);

    public async ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var listed = await store.List(cancellationToken).ConfigureAwait(false);
        if (listed.IsError)
        {
            return ConnectionFailures.Safe(listed.Error);
        }

        var refreshed = 0;
        var failed = 0;
        foreach (var connection in listed.Value.Where(static c => c.Kind == ConnectionKind.OAuth && c.Status == ConnectionStatus.Connected && c.SecretPointer is not null))
        {
            var step = await Step(connection, run.Grant, cancellationToken).ConfigureAwait(false);
            if (step.IsError)
            {
                return step.Error;
            }

            refreshed += step.Value == Outcome.Refreshed ? 1 : 0;
            failed += step.Value == Outcome.NeedsSignIn ? 1 : 0;
        }

        return refreshed + failed == 0 ? ScheduledResult.Quiet : new ScheduledResult(Idle: false, string.Create(CultureInfo.InvariantCulture, $"refreshed {refreshed}, need a new sign-in {failed}"));
    }

    // One connection: left alone unless its access token ends within the horizon; a provider's refusal is an outcome, a store or ledger error is the run's error.
    private async ValueTask<Result<Outcome>> Step(Connection connection, LeaseGrant grant, CancellationToken cancellationToken)
    {
        if (connection.SecretPointer is not { } pointer)
        {
            return Outcome.Skipped;
        }

        var tokens = secrets.Read(pointer);
        var preset = connection.Preset is { } key ? presets.Find(key) : null;
        if (tokens.IsError || preset is null || tokens.Value.ExpiresAt is not { } expires || expires - clock.GetUtcNow() > _horizon)
        {
            return Outcome.Skipped;   // an unreadable secret is retried next tick; it is not the provider's word
        }

        var refresh = tokens.Value.RefreshToken is { Length: > 0 } token
            ? await oauth.Refresh(preset, connection.Config.GetValueOrDefault("clientId", string.Empty), token, tokens.Value.ClientSecret, cancellationToken).ConfigureAwait(false)
            : SindriErrors.ConnectionNeedsSignIn.With("the connection has no refresh token");
        return refresh.IsError
            ? await Fail(connection, ReasonOf(refresh.Error), grant, cancellationToken).ConfigureAwait(false)
            : await Replace(connection, tokens.Value, refresh.Value, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<Outcome>> Replace(Connection connection, TokenSet old, TokenSet fresh, CancellationToken cancellationToken)
    {
        // a provider that sends no new refresh token keeps the old one; the client secret is stored with the tokens and must survive every refresh
        var next = fresh with { RefreshToken = fresh.RefreshToken ?? old.RefreshToken, ClientSecret = old.ClientSecret };
        var pointer = secrets.Store(connection.Name, next);
        if (pointer.IsError)
        {
            return ConnectionFailures.Safe(pointer.Error);
        }

        var now = clock.GetUtcNow();
        var written = await store.Update(connection with { SecretPointer = pointer.Value, LastRefreshedAt = now, UpdatedAt = now }, cancellationToken).ConfigureAwait(false);
        return written.IsError ? ConnectionFailures.Safe(written.Error) : Outcome.Refreshed;
    }

    private async ValueTask<Result<Outcome>> Fail(Connection connection, string reason, LeaseGrant grant, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var entry = events.Entry(EventTypes.ConnectionNeedsSignIn, new ConnectionNeedsSignIn(connection.Name.ToString(), reason, now), string.Create(CultureInfo.InvariantCulture, $"{EventTypes.ConnectionNeedsSignIn}:{connection.Name}:{now.UtcTicks}"));
        var appended = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);   // first, so a failed append leaves the row as it was and the next tick tries again
        if (appended.IsError)
        {
            return appended.Error;
        }

        var written = await store.Update(connection with { Status = ConnectionStatus.NeedsSignIn, StatusReason = $"the provider refused the refresh ({reason})", UpdatedAt = now }, cancellationToken).ConfigureAwait(false);
        return written.IsError ? ConnectionFailures.Safe(written.Error) : Outcome.NeedsSignIn;
    }

    // The provider's own error code ('invalid_grant', a short [a-z_] word the client checked), else the SND code; never the message.
    private static string ReasonOf(Error error)
    {
        foreach (var (name, value) in error.Extensions ?? ErrorExtensions.Empty)
        {
            if (name == "providerCode" && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } code)
            {
                return code;
            }
        }

        return error.Code;
    }

    private enum Outcome
    {
        Skipped,
        Refreshed,
        NeedsSignIn,
    }
}
