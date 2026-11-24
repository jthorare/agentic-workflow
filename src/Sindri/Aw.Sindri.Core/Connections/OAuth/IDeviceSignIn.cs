using Aw.Kernel;

namespace Aw.Sindri.Core.Connections.OAuth;

/// <summary>
/// The device-flow sign-in (RFC 8628) next to <see cref="ISignInSession"/>'s loopback one (first-run spec decision 13, gap 3): no listener and no client secret, so a box with no gh login signs in to GitHub
/// through Sindri's own OAuth app. It writes nothing: the caller stores the token through the secret store.
/// </summary>
public interface IDeviceSignIn
{
    /// <summary>Asks the provider for a device code.</summary>
    /// <param name="preset">The provider; its <see cref="ConnectionPreset.DeviceCodeUrl"/> names the endpoint.</param>
    /// <param name="client">The OAuth app's client id (<see cref="OAuthClientIds"/>).</param>
    /// <param name="scopes">The scopes; empty takes the preset's defaults.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The code to show; SND-SETUP-013 when the provider refuses the app, SND-CONN-012 for an answer of the wrong shape.</returns>
    public ValueTask<Result<DeviceAuthorization>> Start(ConnectionPreset preset, string client, IReadOnlyList<string> scopes, CancellationToken cancellationToken);

    /// <summary>Polls the token endpoint at the provider's interval (slower after slow_down) until the owner approves, denies or the code expires.</summary>
    /// <param name="preset">The provider.</param>
    /// <param name="client">The same client id as <see cref="Start"/>.</param>
    /// <param name="authorization">What <see cref="Start"/> returned.</param>
    /// <param name="cancellationToken">Cancels the polling (the owner's Cancel).</param>
    /// <returns>The tokens; SND-CONN-003 when the code expired, SND-CONN-009 when the owner denied, SND-SETUP-013 when the provider refuses the app, SND-CONN-012 for an answer of the wrong shape.</returns>
    public ValueTask<Result<TokenSet>> Complete(ConnectionPreset preset, string client, DeviceAuthorization authorization, CancellationToken cancellationToken);
}
