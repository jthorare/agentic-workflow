using Aw.Kernel;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.OAuth;
using Aw.Sindri.Core.Profile;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.ConnectAccount;

/// <summary>A stub until the device-flow wiring commit (Part of #3321).</summary>
/// <param name="device">The device-flow sign-in.</param>
/// <param name="presets">The github preset.</param>
/// <param name="profiles">The live profile's `setup.oauth`.</param>
/// <param name="scopes">A scope for the background write.</param>
internal sealed class DeviceAccountSignIn(IDeviceSignIn device, IConnectionPresetCatalog presets, IProfileSource profiles, IServiceScopeFactory scopes)
{
    /// <summary>Starts GitHub's device flow and polls it in the background.</summary>
    /// <param name="actor">Who connects (the connection row's CreatedBy).</param>
    /// <param name="cancellationToken">Cancels the start call; the background poll runs on.</param>
    /// <returns>The `code` answer, a `token-needed` answer, or the error.</returns>
    internal ValueTask<Result<ConnectAccountResponse>> Start(string actor, CancellationToken cancellationToken)
    {
        GC.KeepAlive((device, presets, profiles, scopes, actor, cancellationToken));
        throw new NotSupportedException("the device-flow wiring is not built yet (Part of #3321)");
    }
}
