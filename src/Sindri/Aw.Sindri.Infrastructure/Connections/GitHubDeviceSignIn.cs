using Aw.Kernel;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.OAuth;

namespace Aw.Sindri.Infrastructure.Connections;

/// <summary>A stub until the device-flow commit (Part of #3321).</summary>
/// <param name="http">Sends the device and token requests.</param>
/// <param name="clock">Times the polls and the expiry.</param>
internal sealed class GitHubDeviceSignIn(HttpClient http, TimeProvider clock) : IDeviceSignIn
{
    /// <inheritdoc />
    public ValueTask<Result<DeviceAuthorization>> Start(ConnectionPreset preset, string client, IReadOnlyList<string> scopes, CancellationToken cancellationToken)
    {
        GC.KeepAlive((http, clock, preset, client, scopes, cancellationToken));
        throw new NotSupportedException("the device flow is not built yet (Part of #3321)");
    }

    /// <inheritdoc />
    public ValueTask<Result<TokenSet>> Complete(ConnectionPreset preset, string client, DeviceAuthorization authorization, CancellationToken cancellationToken)
    {
        GC.KeepAlive((preset, client, authorization, cancellationToken));
        throw new NotSupportedException("the device flow is not built yet (Part of #3321)");
    }
}
