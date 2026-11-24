using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.OAuth;
using Aw.Sindri.Core.Profile;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Features.ConnectAccount;

/// <summary>
/// GitHub's device flow for the accounts step (first-run spec decision 13, §4.2): picks the client id (`setup.oauth.github`, else the shipped one), asks GitHub for a code, answers `code` at once and polls in
/// the background. One sign-in runs at a time: a new start or <see cref="Cancel"/> ends the one before. On GitHub's yes, <paramref name="save"/> stores the token. No client id, or an
/// app GitHub refuses, answers `token-needed` with SND-SETUP-014 or -013 as the reason. The device code and the token stay in memory and the secret store only.
/// </summary>
/// <param name="device">The device-flow sign-in.</param>
/// <param name="presets">The github preset.</param>
/// <param name="profiles">The live profile's `setup.oauth`.</param>
/// <param name="save">Stores GitHub's yes (<see cref="DeviceAccountStore"/>, in a scope of its own that the registration opens).</param>
/// <param name="logger">Logs a failed poll by its code only; null logs nothing.</param>
internal sealed partial class DeviceAccountSignIn(IDeviceSignIn device, IConnectionPresetCatalog presets, IProfileSource profiles, DeviceAccountSave save, ILogger<DeviceAccountSignIn>? logger = null) : IDisposable
{
    private const string Provider = "github";
    private readonly Lock _gate = new();
    private CancellationTokenSource? _running;

    /// <summary>Gets the background poll of the newest sign-in, or null before the first (tests wait on it).</summary>
    internal Task? Polling { get; private set; }

    /// <summary>Starts GitHub's device flow and polls it in the background.</summary>
    /// <param name="actor">Who connects (the connection row's CreatedBy).</param>
    /// <param name="cancellationToken">Cancels the start call; the background poll runs on.</param>
    /// <returns>The `code` answer, a `token-needed` answer, or the error.</returns>
    internal async ValueTask<Result<ConnectAccountResponse>> Start(string actor, CancellationToken cancellationToken)
    {
        if (presets.Find(Provider) is not { } preset)
        {
            return SindriErrors.InvalidInput.With("This build has no github connection preset (fix: reinstall Sindri)");
        }

        var loaded = await profiles.Load(cancellationToken).ConfigureAwait(false);
        var client = OAuthClientIds.For(preset, loaded.IsSuccess ? loaded.Value.Profile.Setup?.Oauth : null);
        if (client.IsError)
        {
            return TokenNeeded(client.Error);
        }

        var started = await device.Start(preset, client.Value, [], cancellationToken).ConfigureAwait(false);
        if (started.IsError && IsPasteReason(started.Error))
        {
            return TokenNeeded(started.Error);
        }

        if (started.IsError)
        {
            return started.Error;
        }

        var token = Replace();
        Polling = Poll(preset, client.Value, started.Value, actor, token);
        var code = started.Value;
        return new ConnectAccountResponse(Provider, "code", code.UserCode, code.VerificationUri.ToString(), code.ExpiresAt);
    }

    /// <summary>Ends the sign-in in progress, if any: nothing is stored.</summary>
    /// <returns>True when one was running.</returns>
    internal bool Cancel()
    {
        lock (_gate)
        {
            if (_running is not { } running)
            {
                return false;
            }

            _running = null;
            running.Cancel();
            running.Dispose();
            return true;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            _running?.Cancel();
            _running?.Dispose();
            _running = null;
        }
    }

    private CancellationToken Replace()
    {
        _ = Cancel();
        lock (_gate)
        {
            _running = new CancellationTokenSource();
            return _running.Token;
        }
    }

    // why: nothing awaits this; a failed poll is logged by its code, a cancelled one (Cancel, a newer code) ends quietly with nothing stored
    private async Task Poll(ConnectionPreset preset, string client, DeviceAuthorization authorization, string actor, CancellationToken cancellationToken)
    {
        try
        {
            var tokens = await device.Complete(preset, client, authorization, cancellationToken).ConfigureAwait(false);
            var stored = tokens.IsError ? Results.Failure<Unit>(tokens.Error) : await save(tokens.Value, client, actor, cancellationToken).ConfigureAwait(false);
            if (stored.IsError && logger is not null)
            {
                LogFailed(logger, stored.Error.Code);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // the owner's Cancel or a newer code: the sign-in ends with nothing stored
        }
    }

    private static bool IsPasteReason(Error error) =>
        string.Equals(error.Code, SindriErrors.SetupOauthBlocked.Code, StringComparison.Ordinal) || string.Equals(error.Code, SindriErrors.SetupNoClientId.Code, StringComparison.Ordinal);

    private static ConnectAccountResponse TokenNeeded(Error reason) => new(Provider, "token-needed", Reason: reason.Code);

    [LoggerMessage(Level = LogLevel.Warning, Message = "GitHub's device sign-in ended without a connection: {Code}")]
    private static partial void LogFailed(ILogger logger, string code);
}
