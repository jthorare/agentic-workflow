using System.Globalization;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.OAuth;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Features.RefreshConnections;

/// <summary>
/// The 'connection-refresh' background service (connections PR 10b), every 5 minutes: each 'connected' oauth connection whose access token expires within 10 minutes
/// of now (the clock port, never the wall clock) is refreshed through <see cref="IOAuthClient"/> and its secret replaced, keeping the old refresh token when the provider returns none.
/// Only the provider's refusal of the grant (an auth error code on a 400 or 401, or no refresh token stored) sets the connection to 'needs-sign-in' with that code as the reason
/// (never a token); the status writer then appends ONE 'connection.needs-sign-in' event, which projects the Needs-you row 'connection:&lt;name&gt;'. An unreachable provider, a timeout, a 429,
/// a 5xx or 'temporarily_unavailable' leaves the row 'connected' and the next tick tries again. Other kinds, other statuses and later expiries are left alone.
/// The writes run under the command gate and only if the row and its secret are still what the refresh started from, so a sign-in that finished during the provider call keeps its tokens.
/// An error on one connection (a store or ledger error) is logged by code and the run goes on to the next.
/// </summary>
/// <param name="store">The connections.</param>
/// <param name="secrets">Reads and replaces the tokens.</param>
/// <param name="presets">The shipped presets (the token endpoint).</param>
/// <param name="oauth">Refreshes a token.</param>
/// <param name="writer">Records a refused refresh (status, and the Needs-you event) under the gate.</param>
/// <param name="clock">The time.</param>
/// <param name="logger">Logs a connection that could not be written, by code; null logs nothing.</param>
internal sealed partial class RefreshConnectionsService(IConnectionStore store, IConnectionSecrets secrets, IConnectionPresetCatalog presets, IOAuthClient oauth, ConnectionStatusWriter writer, TimeProvider clock, ILogger<RefreshConnectionsService>? logger = null)
    : IScheduledService
{
    internal static ServiceName ServiceId { get; } = ServiceName.From("connection-refresh");

    /// <summary>What the service does, as GET /sindri/services says it.</summary>
    public const string Description = "Refreshes the tokens of connections that expire within 10 minutes.";

    /// <summary>Runs without a profile entry, every 5 minutes.</summary>
    internal static BuiltInService BuiltIn { get; } = new(ServiceId, new Cadence(TimeSpan.FromMinutes(5)), Description);

    public ServiceName Identity() => ServiceId;

    private static readonly TimeSpan _horizon = TimeSpan.FromMinutes(10);

    // A provider that answers busy says so with these on a 400 or 401 too; they are not a verdict on the grant.
    private static readonly string[] _transientCodes = ["temporarily_unavailable", "slow_down", "server_error"];

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
        var errors = 0;
        foreach (var connection in listed.Value.Where(static c => c.Kind == ConnectionKind.OAuth && c.Status == ConnectionStatus.Connected && c.SecretPointer is not null))
        {
            var step = await Step(connection, run.Grant, cancellationToken).ConfigureAwait(false);
            if (step.IsError)
            {
                errors++;   // this connection only: the others still get their turn, and the next tick tries it again
                if (logger is not null)
                {
                    LogStepFailed(logger, connection.Name, step.Error.Code);
                }

                continue;
            }

            refreshed += step.Value == Outcome.Refreshed ? 1 : 0;
            failed += step.Value == Outcome.NeedsSignIn ? 1 : 0;
        }

        return refreshed + failed + errors == 0 ? ScheduledResult.Quiet : new ScheduledResult(Idle: false, string.Create(CultureInfo.InvariantCulture, $"refreshed {refreshed}, need a new sign-in {failed}, not written {errors}"));
    }

    // One connection: left alone unless its access token ends within the horizon; a provider's refusal is an outcome, a store or ledger error is this connection's error.
    private async ValueTask<Result<Outcome>> Step(Connection connection, LeaseGrant grant, CancellationToken cancellationToken)
    {
        var preset = connection.Preset is { } key ? presets.Find(key) : null;
        var tokens = connection.SecretPointer is { } pointer ? secrets.Read(pointer) : null;
        if (tokens is not { IsSuccess: true } || preset is null || tokens.Value.ExpiresAt is not { } expires || expires - clock.GetUtcNow() > _horizon)
        {
            return Outcome.Skipped;   // an unreadable secret is retried next tick; it is not the provider's word
        }

        return tokens.Value.RefreshToken is not { Length: > 0 }
            ? await Fail(connection, "the connection has no refresh token", grant, cancellationToken).ConfigureAwait(false)
            : await Refresh(connection, preset, tokens.Value, grant, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<Outcome>> Refresh(Connection connection, ConnectionPreset preset, TokenSet old, LeaseGrant grant, CancellationToken cancellationToken)
    {
        var refresh = await oauth.Refresh(preset, connection.Config.GetValueOrDefault("clientId", string.Empty), old.RefreshToken ?? string.Empty, old.ClientSecret, cancellationToken).ConfigureAwait(false);
        if (refresh.IsSuccess)
        {
            var replaced = await writer.ReplaceTokens(connection, old, refresh.Value, cancellationToken).ConfigureAwait(false);
            return replaced.IsError ? replaced.Error : replaced.Value ? Outcome.Refreshed : Outcome.Skipped;   // not replaced: the gate was busy, or a sign-in or removal got there first
        }

        return ProviderCode(refresh.Error) is { } code && !_transientCodes.Contains(code, StringComparer.Ordinal)
            ? await Fail(connection, $"the provider refused the refresh ({code})", grant, cancellationToken).ConfigureAwait(false)
            : Outcome.Skipped;   // unreachable, timed out, 429, 5xx, temporarily_unavailable: the grant is not shown bad, so the row stays connected and the next tick retries
    }

    // The writer takes the gate, re-reads the row and writes only if it is unchanged, then appends the Needs-you event.
    private async ValueTask<Result<Outcome>> Fail(Connection connection, string reason, LeaseGrant grant, CancellationToken cancellationToken)
    {
        var written = await writer.Record(connection, ConnectionStatus.NeedsSignIn, reason, grant, cancellationToken).ConfigureAwait(false);
        if (written.IsError)
        {
            return written.Error.Code == SindriErrors.CommandBusy.Code ? Outcome.Skipped : written.Error;
        }

        return written.Value.Status == ConnectionStatus.NeedsSignIn && written.Value.StatusReason == reason ? Outcome.NeedsSignIn : Outcome.Skipped;
    }

    // The provider's own error code ('invalid_grant', a short [a-z_] word the client checked), only when the provider gave one on a 400 or 401.
    private static string? ProviderCode(Error error)
    {
        foreach (var (name, value) in error.Extensions ?? ErrorExtensions.Empty)
        {
            if (name == "providerCode" && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } code)
            {
                return code;
            }
        }

        return null;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The refresh of connection {Name} could not be written ({Code})")]
    private static partial void LogStepFailed(ILogger logger, ConnectionName name, string code);

    private enum Outcome
    {
        Skipped,
        Refreshed,
        NeedsSignIn,
    }
}
