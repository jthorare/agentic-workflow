using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.CheckSandbox;

/// <summary>Prepares the login Check again's fixture requests carry, the way a launch does (issue #1579).</summary>
internal interface ISandboxFixtureLogin
{
    /// <summary>Prepares a login for <paramref name="launch"/>; null when no agent is picked for Steps (Steps then go to the desk, so there is nothing to confine) or the login can't be prepared (the login probe reports that as SND-LAUNCH-017).</summary>
    /// <param name="launch">The fixture's launch.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The login, or null.</returns>
    public ValueTask<FixtureLogin?> Prepare(LaunchId launch, CancellationToken cancellationToken);

    /// <summary>Releases a prepared login.</summary>
    /// <param name="login">The login.</param>
    public void Release(PreparedLogin login);
}
