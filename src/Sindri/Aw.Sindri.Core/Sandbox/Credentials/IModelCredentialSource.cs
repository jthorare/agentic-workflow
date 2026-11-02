using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Sandbox.Credentials;

/// <summary>
/// Supplies a target's model login to a sandboxed session (sandbox spec SB18, owner decision Q2). One adapter per source:
/// owner-copy (SB-2) and separate-login (SB-8). Adapters are registered by <see cref="Id"/>; an unknown id is a profile error.
/// A source that can't deliver is SND-LAUNCH-017 with its id: nothing falls back to another source or the owner's login.
/// </summary>
public interface IModelCredentialSource
{
    /// <summary>Gets the id the profile's 'sandbox.login.source' names.</summary>
    public ModelLoginSource Id { get; }

    /// <summary>Whether this source has an adapter for <paramref name="target"/>.</summary>
    /// <param name="target">The agent.</param>
    /// <returns>True when it does.</returns>
    public bool Supports(AgentId target);

    /// <summary>Checks the login cheaply, with no side effect.</summary>
    /// <param name="target">The agent.</param>
    /// <returns>Ready, missing or expired, with detail.</returns>
    public LoginStatus Probe(AgentId target);

    /// <summary>Prepares the login for one launch.</summary>
    /// <param name="target">The agent.</param>
    /// <param name="launch">The launch.</param>
    /// <param name="ct">Cancels the work.</param>
    /// <returns>The prepared login, or SND-LAUNCH-017.</returns>
    public Task<Result<PreparedLogin>> Prepare(AgentId target, LaunchId launch, CancellationToken ct);

    /// <summary>Removes what <see cref="Prepare"/> made (at run close, and by the crash sweep).</summary>
    /// <param name="handle">The prepared login's handle.</param>
    public void Release(LoginHandle handle);
}
