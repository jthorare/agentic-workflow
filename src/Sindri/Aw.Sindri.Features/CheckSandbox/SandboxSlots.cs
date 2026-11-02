using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Features.CheckSandbox;

/// <summary>The free session slots: the approved profile's 'sandbox.limits.sessions' (default 3) less the live scopes. Without a scope reader (no mechanism is registered) nothing is live.</summary>
/// <param name="approved">The approved profile.</param>
/// <param name="scopes">The live scopes, or null when this box has no mechanism.</param>
internal sealed class SandboxSlots(IApprovedProfile approved, ISliceScopes? scopes = null) : ISandboxSlots
{
    public async ValueTask<int> Free(CancellationToken cancellationToken)
    {
        var profile = await approved.Latest(cancellationToken).ConfigureAwait(false);
        var cap = (profile.IsSuccess ? profile.Value?.Profile.Sandbox?.Limits?.Sessions : null) ?? SandboxDefaults.Sessions;
        var live = scopes is null ? 0 : (await scopes.Live(cancellationToken).ConfigureAwait(false)).Count;
        return Math.Max(cap - live, 0);
    }
}
