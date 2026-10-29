using Aw.Kernel;
using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Core.Setup;

/// <summary>
/// Adds one allowed host or home path to a repo's 'sandbox.repos.&lt;repo&gt;' in the live profile file (sandbox spec §3.6, "Allow is a draft profile edit plus the approve flow"): the file is edited as text so every other key and comment
/// stays, written only if it is still what was read, and left for the owner to approve like any other change.
/// </summary>
public interface IProfileSandboxWriter
{
    /// <summary>Allows <paramref name="target"/> for <paramref name="repo"/>.</summary>
    /// <param name="repo">The repo's name.</param>
    /// <param name="kind">Network adds the host to 'hosts'; Path adds the home entry (read-only) to 'homeAllow'.</param>
    /// <param name="target">The host, or the absolute path.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>What was written, SND-SANDBOX-001 when the target is not something the profile can hold (a path outside the home), SND-PROFILE-002 when the file has a layout Sindri won't edit or can't be written, SND-PROFILE-001 when it can't be read.</returns>
    public ValueTask<Result<SandboxAllowed>> Allow(string repo, DenialKind kind, string target, CancellationToken cancellationToken);
}
