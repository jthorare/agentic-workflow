using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Features.TestConnection;

/// <summary>
/// Tries a stored credential the way its kind is checked. A rest (key) connection with a known preset asks the same check the add ran (#4361, <see cref="IKeyCheck"/>): Slack's auth.test reads
/// 'ok', Linear asks for its viewer, so a revoked key fails as SND-CONN-018 and an unreachable service as SND-CONN-019. An empty stored key is SND-CONN-018 with no call. Any other connection, and a rest one whose preset has no check, calls its probe URL (Config 'probeUrl', else the preset's).
/// </summary>
/// <param name="presets">The shipped presets.</param>
/// <param name="probe">Calls the probe URL.</param>
/// <param name="keys">Checks a key with its preset's service.</param>
internal sealed class KeyedCredentialCheck(IConnectionPresetCatalog presets, IConnectionProbe probe, IKeyCheck keys)
{
    /// <summary>Tries the credential.</summary>
    /// <param name="connection">The connection.</param>
    /// <param name="tokens">Its stored secret.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Unit when the service takes it, the error when not, or null when there is nothing to try it against (no preset check and no probe URL).</returns>
    internal async ValueTask<Result<Unit>?> Try(Connection connection, TokenSet tokens, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        var preset = connection.Preset is { } key ? presets.Find(key) : null;
        if (connection.Kind == ConnectionKind.Rest && preset is not null && HasKeyCheck(preset))
        {
            return string.IsNullOrWhiteSpace(tokens.AccessToken)
                ? SindriErrors.ConnectionKeyRefused.With("the stored key is empty (fix: remove the connection and add it again with the key)")
                                : Unit(await keys.Check(preset, tokens.AccessToken, cancellationToken).ConfigureAwait(false));
        }

        var url = connection.Config.GetValueOrDefault("probeUrl") ?? preset?.ProbeUrl;
        return url is null ? null : await probe.Probe(url, tokens, cancellationToken).ConfigureAwait(false);
    }

    private static Result<Unit> Unit(Result<KeyCheckOutcome> outcome) => outcome.IsError ? outcome.Error : Aw.Kernel.Unit.Value;

    // The presets IKeyCheck asks a service about (HttpKeyCheck's rule): Linear's viewer query, or any preset's probe URL. For any other preset the check calls nothing and answers Unit, so the
    // test must say it tried nothing rather than "connected".
    private static bool HasKeyCheck(ConnectionPreset preset) => string.Equals(preset.Key, "linear", StringComparison.Ordinal) || preset.ProbeUrl is not null;
}
